namespace MusicMover;

public class SimilarFileInfo
{
    public FileInfo File { get; set; }
    public MediaFileInfo? MediaInfo { get; set; }

    public SimilarFileInfo(FileInfo file, MediaFileInfo mediaInfo )
    {
        this.File = file;
        this.MediaInfo = mediaInfo;
    }
    public SimilarFileInfo(FileInfo file)
    {
        this.File = file;
    }
}