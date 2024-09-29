namespace MusicMover;

public class SimilarFileInfo
{
    public FileInfo File { get; set; }
    public TagLib.File Tag { get; set; }

    public SimilarFileInfo(FileInfo file, TagLib.File tag)
    {
        this.File = file;
        this.Tag = tag;
    }
}