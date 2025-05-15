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
    private readonly List<string> _providerTypes;
    
    public MiniMediaMetadataAPIService(string baseUrl, List<string> providerTypes)
    {
        _baseUrl = baseUrl;
        _providerTypes = providerTypes;
    }

    public SearchArtistResponse? SearchArtists(string searchTerm)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal SearchResults '{searchTerm}'");
        
        using RestClient client = new RestClient(_baseUrl + "/api/SearchArtist");

        return retryPolicy.Execute(() =>
        {
            RestRequest request = new RestRequest();
            request.AddParameter("Provider", _providerTypes.Count > 1 ? "Any" : _providerTypes.First());
            request.AddParameter("Name", searchTerm);
            request.AddParameter("Offset", 0);
            
            return client.Get<SearchArtistResponse>(request);
        });
    }
    public SearchTrackResponse? SearchTracks(string searchTerm, string artistId, string providerType)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal SearchResults '{searchTerm}'");
        using RestClient client = new RestClient(_baseUrl + "/api/SearchTrack");

        return retryPolicy.Execute(() =>
        {
            RestRequest request = new RestRequest();
            request.AddParameter("Provider", providerType);
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