using System.Text.Json.Serialization;

namespace MusicMover.Models.MusicBrainz;

public class MusicBrainzReleaseMediaModel
{
    [JsonPropertyName("track-count")]
    public int? TrackCount { get; set; }
    
    public string? Format { get; set; }
    public string? Title { get; set; }
    public int? Position { get; set; }
    
    [JsonPropertyName("track-offset")]
    public int? TrackOffset { get; set; }
    
    public List<MusicBrainzReleaseMediaTrackModel>? Tracks { get; set; } = new List<MusicBrainzReleaseMediaTrackModel>();
}