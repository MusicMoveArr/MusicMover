using System.Text.Json.Serialization;

namespace MusicMover.Models.MusicBrainz;

#pragma warning disable CS8618
public class MusicBrainzReleaseGroupModel
{
    [JsonPropertyName("primary-type-id")]
    public string PrimaryTypeId { get; set; }
    
    [JsonPropertyName("secondary-type-ids")]
    public List<string> SecondaryTypeIds { get; set; }
    
    [JsonPropertyName("primary-type")]
    public string PrimaryType { get; set; }
    
    public string Disambiguation { get; set; }
    
    public string Id { get; set; }
    
    [JsonPropertyName("first-release-date")]
    public string FirstReleaseDate { get; set; }
    
    public List<string> Releases { get; set; }
    
    public string Title { get; set; }
    
    [JsonPropertyName("secondary-types")]
    public List<string> SecondaryTypes { get; set; }
}