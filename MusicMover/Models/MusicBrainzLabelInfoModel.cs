using System.Text.Json.Serialization;

namespace MusicMover.Models;

public class MusicBrainzLabelInfoModel
{
    [JsonPropertyName("catalog-number")]
    public string CataLogNumber { get; set; }
    public MusicBrainzLabelInfoLabelModel Label { get; set; }
}