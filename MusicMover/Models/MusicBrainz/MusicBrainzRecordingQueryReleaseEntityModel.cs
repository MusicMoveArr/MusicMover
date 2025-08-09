using System.Text.Json.Serialization;

namespace MusicMover.Models.MusicBrainz;

#pragma warning disable CS8618
public class MusicBrainzRecordingQueryReleaseEntityModel
{
    public string Id { get; set; }
    
    [JsonPropertyName("status-id")]
    public string StatusId { get; set; }
    
    public int Count { get; set; }
    public string Title { get; set; }
    public string Status { get; set; }
    public string Date { get; set; }
    public string Country { get; set; }
    
    [JsonPropertyName("track-count")]
    public int TrackCount { get; set; }
    
    [JsonPropertyName("artist-credit")]
    public List<MusicBrainzArtistCreditModel>? ArtistCredit {get; set; }
    
    [JsonPropertyName("release-group")]
    public MusicBrainzReleaseGroupModel ReleaseGroup { get; set; }
    
    public List<MusicBrainzRecordingQueryReleaseMediaEntityModel> Media {get; set; }
    
}