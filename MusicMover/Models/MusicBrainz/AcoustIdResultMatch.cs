namespace MusicMover.Models.MusicBrainz;

public class AcoustIdResultMatch
{
    public required MusicBrainzArtistCreditModel ArtistCredit {get; init; }
    public required List<string> ISRCS { get; init; } = new List<string>();
    public required MusicBrainzReleaseMediaModel? ReleaseMedia { get; init; }
    public required string? AcoustId { get; set; }
    public required MusicBrainzArtistReleaseModel? Release { get; init; }
    public required List<MusicBrainzArtistCreditModel> ArtistCredits { get; init; }
    public required string? RecordingId { get; init; }
}