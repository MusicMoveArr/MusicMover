namespace MusicMover.Models;

public class AcoustIdResultResponse
{
    public string Id { get; set; }
    public List<AcoustIdRecordingResponse>? Recordings { get; set; }
    public float Score { get; set; }
}