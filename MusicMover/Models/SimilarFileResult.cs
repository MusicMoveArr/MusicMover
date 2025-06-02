namespace MusicMover.Models;

public class SimilarFileResult
{
    public List<SimilarFileInfo> SimilarFiles { get; set; } = new List<SimilarFileInfo>();
    public bool Errors { get; set; }
}