namespace MusicMover.Models.MusicBrainz;

public class GetDataByAcoustIdResult
{
    public AcoustIdRecordingResponse? MatchedRecording { get; set; }
    public string? RecordingId { get; set; }
    public string? AcoustId { get; set; }
    public bool Success { get; set; }
}