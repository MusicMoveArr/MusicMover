namespace MusicMover.Models.AcoustId;

public class AcoustIdReleaseGroups
{
    public string Title { get; set; }
    public List<string> Artists { get; set; }
    public List<AcoustIdReleases> Releases { get; set; }
}