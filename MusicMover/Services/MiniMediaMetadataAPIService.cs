using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using MusicMover.Models.MetadataAPI;
using MusicMover.Models.MetadataAPI.Enums;
using Polly;
using Polly.Retry;
using RestSharp;
using RestSharp.Serializers.Json;

namespace MusicMover.Services;

public class MiniMediaMetadataAPIService
{
    private readonly string _baseUrl;
    private readonly ProviderType _providerType;
    
    public MiniMediaMetadataAPIService(string baseUrl, string providerType)
    {
        _baseUrl = baseUrl;
        if (!Enum.TryParse<ProviderType>(providerType,  out _providerType))
        {
            _providerType = ProviderType.Any;
        }
    }

    public SearchArtistResponse? SearchArtists(string searchTerm)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal SearchResults '{searchTerm}'");
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } // or .Default for "Album", etc.
        };
        
        using RestClient client = new RestClient(_baseUrl + "/api/SearchArtist");

        return retryPolicy.Execute(() =>
        {
            RestRequest request = new RestRequest();
            request.AddParameter("Provider", _providerType.ToString());
            request.AddParameter("Name", searchTerm);
            request.AddParameter("Offset", 0);
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };
            
            return client.Get<SearchArtistResponse>(request);
        });
    }
    public SearchTrackResponse? SearchTracks(string searchTerm, string  artistId)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal SearchResults '{searchTerm}'");
        using RestClient client = new RestClient(_baseUrl + "/api/SearchTrack");

        return retryPolicy.Execute(() =>
        {
            RestRequest request = new RestRequest();
            request.AddParameter("Provider", _providerType.ToString());
            request.AddParameter("TrackName", searchTerm);
            request.AddParameter("ArtistId", artistId);
            request.AddParameter("Offset", 0);
            
            return client.Get<SearchTrackResponse>(request);
        });
    }
    
    private RetryPolicy GetRetryPolicy()
    {
        RetryPolicy retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .WaitAndRetry(5, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) => {
                    Debug.WriteLine($"Retry {retryCount} after {timeSpan.TotalSeconds} sec due to: {exception.Message}");
                    Console.WriteLine($"Retry {retryCount} after {timeSpan.TotalSeconds} sec due to: {exception.Message}");
                });
        
        return retryPolicy;
    }
}