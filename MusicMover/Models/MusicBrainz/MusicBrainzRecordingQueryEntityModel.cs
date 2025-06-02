using System.Text.Json.Serialization;

namespace MusicMover.Models.MusicBrainz;

#pragma warning disable CS8618
public class MusicBrainzRecordingQueryEntityModel
{
    public string Id { get; set; }
    public int Score { get; set; }
    public string Title { get; set; }
    public int Length { get; set; }
    
    [JsonPropertyName("first-release-date")]
    public string FirstReleaseDate { get; set; }
    
    [JsonPropertyName("artist-credit")]
    public List<MusicBrainzArtistCreditModel> ArtistCredit {get; set; }
    
    
    [JsonPropertyName("isrcs")]
    public List<string> ISRCS { get; set; } = new List<string>();
    public List<MusicBrainzRecordingQueryReleaseEntityModel> Releases { get; set; }
}