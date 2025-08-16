namespace MusicMover.Models.AcoustId;

public class AcoustIdResult
{
    public string? Id { get; set; }
    public List<AcoustIdRecording>? Recordings { get; set; }
    public float Score { get; set; }
}