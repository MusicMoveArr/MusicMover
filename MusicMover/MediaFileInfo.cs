using ATL;
using FFMpegCore;

namespace MusicMover;

public class MediaFileInfo
{
    private const string AcoustidFingerprintTag = "Acoustid Fingerprint";
    private const string AcoustIdIdTag = "AcoustidId";
    private const string AcoustIdTag = "AcoustId";
    
    public FileInfo? FileInfo { get; set; }
    
    public string? Artist { get; set; }
    public string? SortArtist { get; set; }
    public string? Title { get; set; }
    public string? Album { get; set; }
    public int? Track { get; set; }
    public int? TrackCount { get; set; }
    public string? AlbumArtist { get; set; }
    public string? AcoustId { get; set; }
    public string? AcoustIdFingerPrint { get; set; }
    public double BitRate { get; set; }
    public int Disc { get; set; }
    public int Duration { get; set; }

    public MediaFileInfo()
    {
        
    }
    
    public MediaFileInfo(FileInfo fileInfo)
    {
        this.FileInfo = fileInfo;
        
        Track trackInfo = new Track(fileInfo.FullName);
        var mediaTags = trackInfo.AdditionalFields
            .GroupBy(pair => pair.Key.ToLower())
            .Select(pair => pair.First())
            .ToDictionary(StringComparer.OrdinalIgnoreCase);
        
        //add all non-AdditionalFields
        trackInfo
            .GetType()
            .GetProperties()
            .ToList()
            .ForEach(prop =>
            {
                object? value = prop.GetValue(trackInfo);
                
                if (value is not null &&
                    (value is string || (value is int val && val > 0)) &&
                    !mediaTags.ContainsKey(prop.Name))
                {
                    mediaTags[prop.Name] = value.ToString();
                }
            });
        
        this.Title = mediaTags.FirstOrDefault(tag => string.Equals(tag.Key, "title", StringComparison.OrdinalIgnoreCase)).Value;
        this.Album = mediaTags.FirstOrDefault(tag => string.Equals(tag.Key, "album", StringComparison.OrdinalIgnoreCase)).Value;
        this.Duration = trackInfo.Duration;
        
        string track = mediaTags.FirstOrDefault(tag => 
            string.Equals(tag.Key, "track", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag.Key, "tracknumber", StringComparison.OrdinalIgnoreCase)
            ).Value;
        
        if (track?.Contains('/') == true)
        {
            this.Track = int.Parse(track.Split('/')[0]);
            this.TrackCount = int.Parse(track.Split('/')[1]);
        }
        else
        {
            int.TryParse(mediaTags.FirstOrDefault(tag => string.Equals(tag.Key, "tracktotal", StringComparison.OrdinalIgnoreCase)).Value, out int trackTotal);
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
            string.Equals(tag.Key, "albumartist", StringComparison.OrdinalIgnoreCase)).Value;
        
        this.SortArtist = mediaTags.FirstOrDefault(tag => 
            string.Equals(tag.Key, "artistsort", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag.Key, "artist_sort", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag.Key, "sortartist", StringComparison.OrdinalIgnoreCase)).Value;
        
        this.Artist = mediaTags.FirstOrDefault(tag => string.Equals(tag.Key, "artist", StringComparison.OrdinalIgnoreCase)).Value;

        this.BitRate = trackInfo.Bitrate;
        AcoustIdFingerPrint = mediaTags.FirstOrDefault(tag => string.Equals(tag.Key,AcoustidFingerprintTag, StringComparison.OrdinalIgnoreCase)).Value;
        AcoustId = mediaTags.FirstOrDefault(tag => 
            string.Equals(tag.Key.Replace(" ", string.Empty), AcoustIdIdTag, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag.Key.Replace(" ", string.Empty), AcoustIdTag, StringComparison.OrdinalIgnoreCase)
            ).Value;
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