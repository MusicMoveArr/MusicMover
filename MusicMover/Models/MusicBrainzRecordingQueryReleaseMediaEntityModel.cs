using System.Text.Json.Serialization;

namespace MusicMover.Models;

public class MusicBrainzRecordingQueryReleaseMediaEntityModel
{
    [JsonPropertyName("track-count")]
    public int? TrackCount { get; set; }
    
    public string? Format { get; set; }
    public int? Position { get; set; }
    
    [JsonPropertyName("track-offset")]
    public int? TrackOffset { get; set; }
    
    public List<MusicBrainzRecordingQueryReleaseMediaTrackEntityModel>? Track { get; set; } = new List<MusicBrainzRecordingQueryReleaseMediaTrackEntityModel>();
}