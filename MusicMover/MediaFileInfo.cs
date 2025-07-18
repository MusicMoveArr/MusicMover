using System.Globalization;
using ATL;
using FFMpegCore;
using MusicMover.Models;
using MusicMover.Services;

namespace MusicMover;

public class MediaFileInfo
{
    private const string AcoustidFingerprintTag = "Acoustid Fingerprint";
    private const string AcoustidFingerprintDurationTag = "Acoustid Fingerprint Duration";
    private const string AcoustIdIdTag = "AcoustidId";
    private const string AcoustIdTag = "AcoustId";
    
    public Track TrackInfo { get; private set; }
    public FileInfo FileInfo { get; set; }
    private FingerPrintService _fingerPrintService;
    
    public string? Artist { get; set; }
    public string? SortArtist { get; set; }
    public string? Title { get; set; }
    public string? Album { get; set; }
    public int? Track { get; set; }
    public int? TrackCount { get; set; }
    public string? AlbumArtist { get; set; }
    public string? AcoustId { get; set; }
    public string? AcoustIdFingerPrint { get; set; }
    public float? AcoustIdFingerPrintDuration { get; set; }
    public double BitRate { get; set; }
    public int Disc { get; set; }
    public int Duration { get; set; }

    public MediaFileInfo()
    {
        this._fingerPrintService = new FingerPrintService();
    }
    
    public MediaFileInfo(FileInfo fileInfo)
        : this()
    {
        this.FileInfo = fileInfo;
        
        CancellationTokenSource cancellationToken = new CancellationTokenSource();
        var readRask = Task.Run(() => this.TrackInfo = new Track(fileInfo.FullName), cancellationToken.Token);
        Task.WhenAny(readRask, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();
        
        if (this.TrackInfo == null)
        {
            try
            {
                cancellationToken.Cancel();
            }
            catch { }
            
            throw new FileLoadException($"It took too long to load '{fileInfo.FullName}'");
        }
        
        var mediaTags = TrackInfo.AdditionalFields
            .GroupBy(pair => pair.Key.ToLower())
            .Select(pair => pair.First())
            .ToDictionary(StringComparer.OrdinalIgnoreCase);
        
        //add all non-AdditionalFields
        TrackInfo
            .GetType()
            .GetProperties()
            .ToList()
            .ForEach(prop =>
            {
                object? value = prop.GetValue(TrackInfo);
                
                if (value is not null &&
                    (value is string || (value is int val && val > 0)) &&
                    !mediaTags.ContainsKey(prop.Name))
                {
                    mediaTags[prop.Name] = value.ToString().Trim();
                }
            });
        
        this.Title = mediaTags.FirstOrDefault(tag => 
            string.Equals(tag.Key, "title", StringComparison.OrdinalIgnoreCase)).Value?.Trim();
        
        this.Album = mediaTags.FirstOrDefault(tag => 
            string.Equals(tag.Key, "album", StringComparison.OrdinalIgnoreCase)).Value?.Trim();
        this.Duration = TrackInfo.Duration;
        
        string? track = mediaTags.FirstOrDefault(tag => 
            string.Equals(tag.Key, "track", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag.Key, "tracknumber", StringComparison.OrdinalIgnoreCase)
            ).Value?.Trim();
        
        if (track?.Contains('/') == true)
        {
            this.Track = int.Parse(track.Split('/')[0]);
            this.TrackCount = int.Parse(track.Split('/')[1]);
        }
        else
        {
            int.TryParse(mediaTags.FirstOrDefault(tag => 
                string.Equals(tag.Key, "tracktotal", StringComparison.OrdinalIgnoreCase)).Value, out int trackTotal);
            
            int.TryParse(mediaTags.FirstOrDefault(tag => 
                string.Equals(tag.Key, "track", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag.Key, "tracknumber", StringComparison.OrdinalIgnoreCase)).Value, out int trackValue);
            
            this.Track = trackValue;
            this.TrackCount = trackTotal;
        }

        int disc = 0;
        string discTag = mediaTags.FirstOrDefault(tag => 
            string.Equals(tag.Key, "disc", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag.Key, "discnumber", StringComparison.OrdinalIgnoreCase)).Value;
        if (discTag?.Contains('/') == true)
        {
            int.TryParse(discTag.Split('/')[0], out disc);
        }
        else
        {
            int.TryParse(discTag, out int discValue);
            disc = discValue;
        }
        this.Disc = disc;
        
        this.AlbumArtist = mediaTags.FirstOrDefault(tag => 
            string.Equals(tag.Key, "album_artist", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag.Key, "albumartist", StringComparison.OrdinalIgnoreCase)).Value?.Trim();
        
        this.SortArtist = mediaTags.FirstOrDefault(tag => 
            string.Equals(tag.Key, "artistsort", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag.Key, "artist_sort", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag.Key, "sortartist", StringComparison.OrdinalIgnoreCase)).Value?.Trim();
        
        this.Artist = mediaTags.FirstOrDefault(tag => 
            string.Equals(tag.Key, "artist", StringComparison.OrdinalIgnoreCase)).Value?.Trim();

        this.BitRate = TrackInfo.Bitrate;
        AcoustIdFingerPrint = mediaTags.FirstOrDefault(tag => 
            string.Equals(tag.Key,AcoustidFingerprintTag, StringComparison.OrdinalIgnoreCase)).Value?.Trim();

        float fingerprintDuration = 0;
        if(float.TryParse(mediaTags.FirstOrDefault(tag => 
               string.Equals(tag.Key,AcoustidFingerprintDurationTag, StringComparison.OrdinalIgnoreCase)).Value, 
               CultureInfo.InvariantCulture, out fingerprintDuration))
        {
            AcoustIdFingerPrintDuration = fingerprintDuration;
        }
        
        AcoustId = mediaTags.FirstOrDefault(tag => 
            string.Equals(tag.Key.Replace(" ", string.Empty), AcoustIdIdTag, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag.Key.Replace(" ", string.Empty), AcoustIdTag, StringComparison.OrdinalIgnoreCase)
            ).Value?.Trim();
    }

    public async Task<bool> GenerateSaveFingerprintAsync()
    {
        if (!string.IsNullOrWhiteSpace(AcoustIdFingerPrint) &&
            AcoustIdFingerPrintDuration > 0)
        {
            return false;
        }

        FpcalcOutput? fingerprint = await _fingerPrintService.GetFingerprintAsync(FileInfo.FullName);
        if (string.IsNullOrWhiteSpace(fingerprint?.Fingerprint))
        {
            return false;
        }

        Track track = new Track(FileInfo.FullName);
        MediaTagWriteService mediaTagWriteService = new MediaTagWriteService();

        bool updated = false;
        string originalValue = string.Empty;
        mediaTagWriteService.UpdateTrackTag(track,
            AcoustidFingerprintTag,
            fingerprint.Fingerprint,
            ref updated,
            ref originalValue);
        
        mediaTagWriteService.UpdateTrackTag(track,
            AcoustidFingerprintDurationTag,
            (fingerprint?.Duration ?? 0).ToString(),
            ref updated,
            ref originalValue);

        return await mediaTagWriteService.SafeSaveAsync(track);
    }

    public void Save(string artist)
    {
        string tempFile = $"{FileInfo.FullName}.tmp{FileInfo.Extension}";
        bool success = FFMpegArguments
            .FromFileInput(FileInfo.FullName)
            .OutputToFile(tempFile, overwrite: true, options => options
                .WithCustomArgument($"-metadata album_artist=\"{artist}\"")
                .WithCustomArgument($"-metadata artist=\"{artist}\"")
                .WithCustomArgument("-codec copy")) // Prevents re-encoding
            .ProcessSynchronously();

        if (success && File.Exists(tempFile))
        {
            File.Move(tempFile, FileInfo.FullName, true);
        }
        else if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
    }
}