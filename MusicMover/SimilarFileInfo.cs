namespace MusicMover;

public class SimilarFileInfo
{
    public FileInfo File { get; set; }
    public CachedMediaInfo MediaInfo { get; set; }

    public SimilarFileInfo(FileInfo file, CachedMediaInfo mediaInfo )
    {
        this.File = file;
        this.MediaInfo = mediaInfo;
    }
}