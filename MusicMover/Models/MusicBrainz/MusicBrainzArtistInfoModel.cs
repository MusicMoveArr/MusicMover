using System.Text.Json.Serialization;

namespace MusicMover.Models.MusicBrainz;

public class MusicBrainzArtistInfoModel
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    
    public string? Country { get; set; }
    
    [JsonPropertyName("sort-name")]
    public string? SortName { get; set; }
    public string? Disambiguation { get; set; }
}