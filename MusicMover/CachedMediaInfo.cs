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
    public string FirstAlbumArtist { get; set; }
    public string FirstPerformer { get; set; }
    public string AcoustId { get; set; }
    public string AcoustIdFingerPrint { get; set; }
    public int BitRate { get; set; }

    public CachedMediaInfo()
    {
        
    }
    public CachedMediaInfo(FileInfo fileInfo)
    {
        var trackInfo = new ATL.Track(fileInfo.FullName);
        this.Title = trackInfo.Title;
        this.Album = trackInfo.Album;
        this.Track = trackInfo.TrackNumber;
        this.TrackCount = trackInfo.TrackTotal;
        this.FirstAlbumArtist = trackInfo.AlbumArtist;
        this.FirstPerformer = trackInfo.Artist;
        this.BitRate = trackInfo.Bitrate;
        AcoustIdFingerPrint = trackInfo.AdditionalFields.FirstOrDefault(field => field.Key == AcoustidFingerprintTag).Value;
        AcoustId = trackInfo.AdditionalFields.FirstOrDefault(field => field.Key == AcoustidTag).Value;
    }
}