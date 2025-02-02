using FFMpegCore;

namespace MusicMover;

public class MediaFileInfo
{
    private const string AcoustidFingerprintTag = "Acoustid Fingerprint";
    private const string AcoustidTag = "Acoustid Id";
    
    public FileInfo FileInfo { get; set; }
    
    public string Artist { get; set; }
    public string SortArtist { get; set; }
    public string Title { get; set; }
    public string Album { get; set; }
    public int? Track { get; set; }
    public int? TrackCount { get; set; }
    public string AlbumArtist { get; set; }
    public string AcoustId { get; set; }
    public string AcoustIdFingerPrint { get; set; }
    public double BitRate { get; set; }
    public int Disc { get; set; }

    public MediaFileInfo()
    {
        
    }
    
    public MediaFileInfo(FileInfo fileInfo)
    {
        this.FileInfo = fileInfo;

        var mediaInfo = FFProbe.Analyse(fileInfo.FullName);
        var mediaTags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var audioStreamTags = mediaInfo.AudioStreams.FirstOrDefault().Tags.ToDictionary(StringComparer.OrdinalIgnoreCase);
        var formatTags = mediaInfo.Format.Tags.ToDictionary(StringComparer.OrdinalIgnoreCase);
        
        foreach (var pair in audioStreamTags)
            mediaTags[pair.Key.ToLower()] = pair.Value;

        foreach (var pair in formatTags)
            mediaTags[pair.Key.ToLower()] = pair.Value;
        
        this.Title = mediaTags.FirstOrDefault(tag => tag.Key == "title").Value;
        this.Album = mediaTags.FirstOrDefault(tag => tag.Key == "album").Value;
        
        string track = mediaTags.FirstOrDefault(tag => tag.Key == "track").Value;
        if (track?.Contains('/') == true)
        {
            this.Track = int.Parse(track.Split('/')[0]);
            this.TrackCount = int.Parse(track.Split('/')[1]);
        }
        else
        {
            int.TryParse(mediaTags.FirstOrDefault(tag => tag.Key == "tracktotal").Value, out int trackTotal);
            int.TryParse(mediaTags.FirstOrDefault(tag => tag.Key == "track").Value, out int trackValue);
            
            this.Track = trackValue;
            this.TrackCount = trackTotal;
        }

        int disc = 0;
        string discTag = mediaTags.FirstOrDefault(tag => tag.Key == "disc").Value;
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
        
        this.AlbumArtist = mediaTags.FirstOrDefault(tag => tag.Key == "album_artist").Value;
        this.SortArtist = mediaTags.FirstOrDefault(tag => tag.Key == "artistsort").Value;

        if (string.IsNullOrWhiteSpace(this.SortArtist))
        {
            this.SortArtist = mediaTags.FirstOrDefault(tag => tag.Key == "sort_artist").Value;
        }
        
        this.Artist = mediaTags.FirstOrDefault(tag => tag.Key == "artist").Value;
        
        this.BitRate = mediaInfo.AudioStreams.FirstOrDefault()?.BitRate ?? 0;
        AcoustIdFingerPrint = mediaTags.FirstOrDefault(tag => tag.Key == AcoustidFingerprintTag.ToLower()).Value;
        AcoustId = mediaTags.FirstOrDefault(tag => tag.Key == AcoustidTag.ToLower()).Value;
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