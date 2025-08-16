using MusicMover.Models.AcoustId;

namespace MusicMover.Models.MusicBrainz;

public class GetDataByAcoustIdResult
{
    public AcoustIdRecording? MatchedRecording { get; set; }
    public string? RecordingId { get; set; }
    public string? AcoustId { get; set; }
    public bool Success { get; set; }
}