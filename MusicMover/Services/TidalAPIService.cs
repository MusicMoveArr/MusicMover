using System.Diagnostics;
using System.Text;
using MusicMover.Helpers;
using MusicMover.Models.Tidal;
using Polly;
using Polly.Retry;
using RestSharp;
using Spectre.Console;

namespace MusicMover.Services;

public class TidalAPIService
{
    private const string AuthTokenUrl = "https://auth.tidal.com/v1/oauth2/token";
    private const string SearchResultArtistsUrl = "https://openapi.tidal.com/v2/searchResults/";
    private const string ArtistsIdUrl = "https://openapi.tidal.com/v2/artists/{0}";
    private const string TracksByAlbumIdUrl = "https://openapi.tidal.com/v2/albums/{0}";
    private const string TracksUrl = "https://openapi.tidal.com/v2/tracks";
    private const string SearchResultsUrl = "https://openapi.tidal.com/v2/searchResults";
    private const string TidalApiPrefix = "https://openapi.tidal.com/v2";

    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _countryCode;

    public TidalAuthenticationResponse? AuthenticationResponse { get; private set; }

    public TidalAPIService(string clientId, string clientSecret, string countryCode)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        _countryCode = countryCode;
    }
    
    public async Task<TidalAuthenticationResponse?> AuthenticateAsync()
    {
        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal Authenticate");
        using RestClient client = new RestClient(AuthTokenUrl);

        var token = await retryPolicy.ExecuteAsync(async () =>
        {
            RestRequest request = new RestRequest();
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            request.AddHeader("Authorization", $"Basic {credentials}");
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("grant_type", "client_credentials");
            
            return await client.PostAsync<TidalAuthenticationResponse>(request);
        });

        if (token != null)
        {
            this.AuthenticationResponse = token;
        }
        return token;
    }
    
    public async Task<TidalSearchResponse?> SearchResultsArtistsAsync(string searchTerm)
    {
        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal SearchResults '{searchTerm}'");
        using RestClient client = new RestClient(SearchResultArtistsUrl + Uri.EscapeDataString(searchTerm));

        return await retryPolicy.ExecuteAsync(async () =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");
            request.AddParameter("countryCode", _countryCode);
            request.AddParameter("include", "artists");
            
            return await client.GetAsync<TidalSearchResponse>(request);
        });
    }
    
    public async Task<TidalSearchResponse?> SearchResultsTracksAsync(string searchTerm)
    {
        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal SearchResults '{searchTerm}'");
        using RestClient client = new RestClient(SearchResultArtistsUrl + Uri.EscapeDataString(searchTerm));

        return await retryPolicy.ExecuteAsync(async () =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");
            request.AddParameter("countryCode", _countryCode);
            request.AddParameter("include", "tracks,artists,albums");
            
            return await client.GetAsync<TidalSearchResponse>(request);
        });
    }
    
    public async Task<TidalSearchResponse?> GetArtistInfoByIdAsync(int artistId)
    {
        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal GetArtistById '{artistId}'");
        using RestClient client = new RestClient(string.Format(ArtistsIdUrl, artistId));

        return await retryPolicy.ExecuteAsync(async () =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");
            request.AddParameter("countryCode", _countryCode);
            request.AddParameter("include", "albums,profileArt");

            return await client.GetAsync<TidalSearchResponse>(request);
        });
    }
    public async Task<TidalSearchArtistNextResponse?> GetAlbumSelfInfoAsync(string selfLink)
    {
        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal GetAlbumSelfInfo ");

        string url = $"{TidalApiPrefix}{selfLink}";
        using RestClient client = new RestClient(url);

        return await retryPolicy.ExecuteAsync(async () =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");
            return await client.GetAsync<TidalSearchArtistNextResponse>(request);
        });
    }
    
    public async Task<TidalSearchArtistNextResponse?> GetArtistNextInfoByIdAsync(int artistId, string next)
    {
        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal GetArtistNextInfoById '{artistId}'");

        string url = $"{TidalApiPrefix}{next}";

        if (!url.Contains("include="))
        {
            url += "&include=albums,profileArt";
        }
        using RestClient client = new RestClient(url);

        return await retryPolicy.ExecuteAsync(async () =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");
            
            return await client.GetAsync<TidalSearchArtistNextResponse>(request);
        });
    }
    
    public async Task<TidalSearchResponse?> GetTracksByAlbumIdAsync(int albumId)
    {
        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal GetTracksByAlbumId '{albumId}'");
        using RestClient client = new RestClient(string.Format(TracksByAlbumIdUrl, albumId));

        return await retryPolicy.ExecuteAsync(async () =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");
            request.AddParameter("countryCode", _countryCode);
            request.AddParameter("include", "artists,coverArt,items,providers");
            
            return await client.GetAsync<TidalSearchResponse>(request);
        });
    }
    
    public async Task<TidalSearchTracksNextResponse?> GetTracksNextByAlbumIdAsync(int albumId, string next)
    {
        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal GetTracksNextByAlbumId '{albumId}'");
        
        string url = $"{TidalApiPrefix}{next}";

        if (!url.Contains("include="))
        {
            url += "&include=artists,coverArt,items,providers";
        }
        using RestClient client = new RestClient(url);

        return await retryPolicy.ExecuteAsync(async () =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");

            return await client.GetAsync<TidalSearchTracksNextResponse>(request);
        });
    }
    
    public async Task<TidalSearchTracksNextResponse?> GetTracksNextFromSearchAsync(string next)
    {
        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal GetTracksNextFromSearch");
        
        string url = $"{TidalApiPrefix}{next}";

        if (!url.Contains("include="))
        {
            url += "&include=tracks,artists,albums";
        }
        using RestClient client = new RestClient(url);

        return await retryPolicy.ExecuteAsync(async () =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");
            
            return await client.GetAsync<TidalSearchTracksNextResponse>(request);
        });
    }
    
    public async Task<TidalSearchTracksNextResponse?> GetAlbumByIdAsync(int albumId)
    {
        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal GetTracksNextByAlbumId '{albumId}'");
        
        string url = $"{TidalApiPrefix}";

        if (!url.Contains("include="))
        {
            url += "&include=artists,coverArt,items,providers";
        }
        using RestClient client = new RestClient(url);

        return await retryPolicy.ExecuteAsync(async () =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");

            return await client.GetAsync<TidalSearchTracksNextResponse>(request);
        });
    }
    
    public async Task<TidalTrackArtistResponse?> GetTrackArtistsByTrackIdAsync(int[] trackIds)
    {
        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal GetTrackArtistsByTrackId for {trackIds.Length} tracks");
        using RestClient client = new RestClient(TracksUrl);

        return await retryPolicy.ExecuteAsync(async () =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");
            request.AddParameter("filter[id]", string.Join(',', trackIds));
            request.AddParameter("include", "artists");
            request.AddParameter("countryCode", _countryCode);
            
            return await client.GetAsync<TidalTrackArtistResponse>(request);
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
                    Logger.WriteLine($"Retry {retryCount} after {timeSpan.TotalSeconds} sec due to: {exception.Message}");
                });
        
        return retryPolicy;
    }
}