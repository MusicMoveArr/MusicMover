namespace MusicMover.Models.MusicBrainz;

public class MusicBrainzReleaseMediaTrackRecordingModel
{
    public string? Title { get; set; }
    public int? Length { get; set; }
    public string? FirstReleaseDate { get; set; }
    public bool Video { get; set; }
    public string? Id { get; set; }
}