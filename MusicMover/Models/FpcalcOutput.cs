using Newtonsoft.Json;

namespace MusicMover.Models;

public class FpcalcOutput
{
    [JsonProperty("duration")]
    public float Duration { get; set; }
    
    [JsonProperty("fingerprint")]
    public string? Fingerprint { get; set; }
}