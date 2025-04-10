namespace MusicMover.Models;

public class AcoustIdRecordingResponse
{
    public string? Id { get; set; }
    public int? Duration { get; set; }
    public string? Title { get; set; }
    public List<AcoustIdArtistsResponse>? Artists { get; set; }
}