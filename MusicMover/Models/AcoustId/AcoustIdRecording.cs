using MusicMover.Models.MusicBrainz;

namespace MusicMover.Models.AcoustId;

public class AcoustIdRecording
{
    public string? Id { get; set; }
    public float? Duration { get; set; }
    public string? Title { get; set; }
    public List<AcoustIdArtists>? Artists { get; set; }
    public List<AcoustIdReleaseGroups> ReleaseGroups { get; set; }
    
    public string AcoustId { get; set; }
    public MusicBrainzArtistReleaseModel? RecordingRelease { get; set; }
}