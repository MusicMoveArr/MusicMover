using System.Text.Json.Serialization;

namespace MusicMover.Models.MusicBrainz;

public class MusicBrainzLabelInfoLabelModel
{
    public string? Name { get; set; }
    public string? Disambiguation { get; set; }
    public string? Id { get; set; }
    
    public string? Type { get; set; }
    
    [JsonPropertyName("sort-name")]
    public string? SortName { get; set; }
    
    [JsonPropertyName("type-id")]
    public string? TypeId { get; set; }
    
    [JsonPropertyName("label-code")]
    public int? LabelCode { get; set; }
}