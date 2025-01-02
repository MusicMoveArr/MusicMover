using FFMpegCore;

namespace MusicMover;

public class CachedMediaInfo
{
    private const string AcoustidFingerprintTag = "Acoustid Fingerprint";
    private const string AcoustidTag = "Acoustid Id";
    
    public string Artist { get; set; }
    public string Title { get; set; }
    public string Album { get; set; }
    public int? Track { get; set; }
    public int? TrackCount { get; set; }
    public string AlbumArtist { get; set; }
    public string AcoustId { get; set; }
    public string AcoustIdFingerPrint { get; set; }
    public double BitRate { get; set; }

    public CachedMediaInfo()
    {
        
    }
    public CachedMediaInfo(FileInfo fileInfo)
    {
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
        
        this.AlbumArtist = mediaTags.FirstOrDefault(tag => tag.Key == "album_artist").Value;
        this.Artist = mediaTags.FirstOrDefault(tag => tag.Key == "artist").Value;
        
        this.BitRate = mediaInfo.AudioStreams.FirstOrDefault()?.BitRate ?? 0;
        AcoustIdFingerPrint = mediaTags.FirstOrDefault(tag => tag.Key == AcoustidFingerprintTag.ToLower()).Value;
        AcoustId = mediaTags.FirstOrDefault(tag => tag.Key == AcoustidTag.ToLower()).Value;
    }
}