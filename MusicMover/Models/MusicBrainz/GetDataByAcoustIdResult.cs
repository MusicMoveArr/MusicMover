namespace MusicMover.Models.MusicBrainz;

public class GetDataByAcoustIdResult
{
    public string? RecordingId { get; set; }
    public string? AcoustId { get; set; }
    public bool Success { get; set; }
}