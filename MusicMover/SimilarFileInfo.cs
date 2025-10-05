using MusicMover.MediaHandlers;

namespace MusicMover;

public class SimilarFileInfo
{
    public FileInfo File { get; set; }
    public MediaHandler? MediaHandler { get; set; }

    public SimilarFileInfo(FileInfo file, MediaHandler mediaHandler )
    {
        this.File = file;
        this.MediaHandler = mediaHandler;
    }
    public SimilarFileInfo(FileInfo file)
    {
        this.File = file;
    }
}