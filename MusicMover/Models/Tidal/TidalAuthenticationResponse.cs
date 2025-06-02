using System.Text.Json.Serialization;

namespace MusicMover.Models.Tidal;

#pragma warning disable CS8618
public class TidalAuthenticationResponse
{
    public string Scope { get; set; }
    
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; }
    
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
    
    public DateTime RequestedAt { get; set; }

    public DateTime ExpiresAt
    {
        get => RequestedAt.AddSeconds(ExpiresIn).AddMinutes(-5);
    }
}