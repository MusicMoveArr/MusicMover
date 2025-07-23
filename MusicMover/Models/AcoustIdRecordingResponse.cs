namespace MusicMover.Models;

public class AcoustIdRecordingResponse
{
    public string? Id { get; set; }
    public float? Duration { get; set; }
    public string? Title { get; set; }
    public List<AcoustIdArtistsResponse>? Artists { get; set; }
    
    public string AcoustId { get; set; }
}