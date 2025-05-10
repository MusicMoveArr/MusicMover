using MusicMover.Models.MetadataAPI.Enums;

namespace MusicMover.Models.MetadataAPI;

public class SearchTrackRequest
{
    public string Provider { get; set; }
    public string TrackId { get; set; }
    public string ArtistId { get; set; }
    public string TrackName { get; set; }
    public int Offset { get; set; }
}