namespace MusicMover.Models.MetadataAPI.Entities;

#pragma warning disable CS8618
public class SearchTrackMusicBrainzEntity
{
    public string ArtistId { get; set; }
    public string RecordingId { get; set; }
    public string RecordingTrackId { get; set; }
    public string ReleaseTrackId { get; set; }
    public string ReleaseArtistId { get; set; }
    public string ReleaseGroupId { get; set; }
    public string ReleaseId { get; set; }
    public string AlbumType { get; set; }
    public string AlbumReleaseCountry { get; set; }
    public string AlbumStatus { get; set; }
    public string TrackMediaFormat { get; set; }
}