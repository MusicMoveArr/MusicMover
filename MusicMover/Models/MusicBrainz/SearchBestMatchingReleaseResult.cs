namespace MusicMover.Models.MusicBrainz;

public class SearchBestMatchingReleaseResult
{
    public MusicBrainzArtistReleaseModel? MatchedRelease { get; set; }
    public MusicBrainzArtistCreditModel? BestMatchedArtist { get; set; }
    public string? ArtistCountry { get; set; }
    public MusicBrainzRecordingQueryEntityModel RecordingQuery { get; set; }
    public bool Success { get; set; }
}