using MusicMover.Models.MetadataAPI.Enums;

namespace MusicMover.Models.MetadataAPI;

#pragma warning disable CS8618
public class SearchTrackRequest
{
    public string Provider { get; set; }
    public string TrackId { get; set; }
    public string ArtistId { get; set; }
    public string TrackName { get; set; }
    public int Offset { get; set; }
}