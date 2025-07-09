using System.Diagnostics;
using MusicMover.Models.MetadataAPI;
using Polly;
using Polly.Retry;
using RestSharp;

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

    public async Task<SearchArtistResponse?> SearchArtistsAsync(string searchTerm)
    {
        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting SearchResults '{searchTerm}'");
        
        using RestClient client = new RestClient(_baseUrl + "/api/SearchArtist");

        return await retryPolicy.ExecuteAsync(async () =>
        {
            RestRequest request = new RestRequest();
            request.AddParameter("Provider", _providerTypes.Count > 1 ? "Any" : _providerTypes.First());
            request.AddParameter("Name", searchTerm);
            request.AddParameter("Offset", 0);
            
            return await client.GetAsync<SearchArtistResponse>(request);
        });
    }
    public async Task<SearchTrackResponse?> SearchTracksAsync(string searchTerm, string artistId, string providerType)
    {
        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting SearchResults '{searchTerm}'");
        using RestClient client = new RestClient(_baseUrl + "/api/SearchTrack");

        return await retryPolicy.ExecuteAsync(async () =>
        {
            RestRequest request = new RestRequest();
            request.AddParameter("Provider", providerType);
            request.AddParameter("TrackName", searchTerm);
            request.AddParameter("ArtistId", artistId);
            request.AddParameter("Offset", 0);
            
            return await client.GetAsync<SearchTrackResponse>(request);
        });
    }
    
    private AsyncRetryPolicy GetRetryPolicy()
    {
        AsyncRetryPolicy retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(5, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) => {
                    Debug.WriteLine($"Retry {retryCount} after {timeSpan.TotalSeconds} sec due to: {exception.Message}");
                    Console.WriteLine($"Retry {retryCount} after {timeSpan.TotalSeconds} sec due to: {exception.Message}");
                });
        
        return retryPolicy;
    }
}