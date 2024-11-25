namespace MusicMover;

public class MediaFileInfo
{
    private const string AcoustidFingerprintTag = "Acoustid Fingerprint";
    private const string AcoustidTag = "Acoustid Id";
    
    public ATL.Track TrackInfo { get; set; }
    public FileInfo FileInfo { get; set; }
    
    public string AcoustId { get; set; }
    public string AcoustIdFingerPrint { get; set; }
    
    public MediaFileInfo(FileInfo fileInfo)
    {
        this.FileInfo = fileInfo;
        
        this.TrackInfo = new ATL.Track(fileInfo.FullName);
        this.AcoustIdFingerPrint = TrackInfo.AdditionalFields
            .FirstOrDefault(field => field.Key == AcoustidFingerprintTag).Value;
        
        this.AcoustId = TrackInfo.AdditionalFields
            .FirstOrDefault(field => field.Key == AcoustidTag).Value;

        
    }
}