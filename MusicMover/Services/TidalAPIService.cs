using System.Diagnostics;
using System.Text;
using MusicMover.Models.Tidal;
using Polly;
using Polly.Retry;
using RestSharp;

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
    
    public TidalAuthenticationResponse? Authenticate()
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal Authenticate");
        using RestClient client = new RestClient(AuthTokenUrl);

        var token = retryPolicy.Execute(() =>
        {
            RestRequest request = new RestRequest();
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            request.AddHeader("Authorization", $"Basic {credentials}");
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("grant_type", "client_credentials");
            
            return client.Post<TidalAuthenticationResponse>(request);
        });

        if (token != null)
        {
            this.AuthenticationResponse = token;
        }
        return token;
    }
    
    public TidalSearchResponse? SearchResultsArtists(string searchTerm)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal SearchResults '{searchTerm}'");
        using RestClient client = new RestClient(SearchResultArtistsUrl + Uri.EscapeDataString(searchTerm));

        return retryPolicy.Execute(() =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");
            request.AddParameter("countryCode", _countryCode);
            request.AddParameter("include", "artists");
            
            return client.Get<TidalSearchResponse>(request);
        });
    }
    
    public TidalSearchResponse? SearchResultsTracks(string searchTerm)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal SearchResults '{searchTerm}'");
        using RestClient client = new RestClient(SearchResultArtistsUrl + Uri.EscapeDataString(searchTerm));

        return retryPolicy.Execute(() =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");
            request.AddParameter("countryCode", _countryCode);
            request.AddParameter("include", "tracks,artists,albums");
            
            return client.Get<TidalSearchResponse>(request);
        });
    }
    
    public TidalSearchResponse? GetArtistInfoById(int artistId)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal GetArtistById '{artistId}'");
        using RestClient client = new RestClient(string.Format(ArtistsIdUrl, artistId));

        return retryPolicy.Execute(() =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");
            request.AddParameter("countryCode", _countryCode);
            request.AddParameter("include", "albums,profileArt");

            return client.Get<TidalSearchResponse>(request);
        });
    }
    public TidalSearchArtistNextResponse? GetAlbumSelfInfo(string selfLink)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal GetAlbumSelfInfo ");

        string url = $"{TidalApiPrefix}{selfLink}";
        using RestClient client = new RestClient(url);

        return retryPolicy.Execute(() =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");
            return client.Get<TidalSearchArtistNextResponse>(request);
        });
    }
    
    public TidalSearchArtistNextResponse? GetArtistNextInfoById(int artistId, string next)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal GetArtistNextInfoById '{artistId}'");

        string url = $"{TidalApiPrefix}{next}";

        if (!url.Contains("include="))
        {
            url += "&include=albums,profileArt";
        }
        using RestClient client = new RestClient(url);

        return retryPolicy.Execute(() =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");
            
            return client.Get<TidalSearchArtistNextResponse>(request);
        });
    }
    
    public TidalSearchResponse? GetTracksByAlbumId(int albumId)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal GetTracksByAlbumId '{albumId}'");
        using RestClient client = new RestClient(string.Format(TracksByAlbumIdUrl, albumId));

        return retryPolicy.Execute(() =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");
            request.AddParameter("countryCode", _countryCode);
            request.AddParameter("include", "artists,coverArt,items,providers");
            
            return client.Get<TidalSearchResponse>(request);
        });
    }
    
    public TidalSearchTracksNextResponse? GetTracksNextByAlbumId(int albumId, string next)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal GetTracksNextByAlbumId '{albumId}'");
        
        string url = $"{TidalApiPrefix}{next}";

        if (!url.Contains("include="))
        {
            url += "&include=artists,coverArt,items,providers";
        }
        using RestClient client = new RestClient(url);

        return retryPolicy.Execute(() =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");

            return client.Get<TidalSearchTracksNextResponse>(request);
        });
    }
    
    public TidalSearchTracksNextResponse? GetTracksNextFromSearch(string next)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal GetTracksNextFromSearch");
        
        string url = $"{TidalApiPrefix}{next}";

        if (!url.Contains("include="))
        {
            url += "&include=tracks,artists,albums";
        }
        using RestClient client = new RestClient(url);

        return retryPolicy.Execute(() =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");
            
            return client.Get<TidalSearchTracksNextResponse>(request);
        });
    }
    
    public TidalSearchTracksNextResponse? GetAlbumById(int albumId)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal GetTracksNextByAlbumId '{albumId}'");
        
        string url = $"{TidalApiPrefix}";

        if (!url.Contains("include="))
        {
            url += "&include=artists,coverArt,items,providers";
        }
        using RestClient client = new RestClient(url);

        return retryPolicy.Execute(() =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");

            return client.Get<TidalSearchTracksNextResponse>(request);
        });
    }
    
    public TidalTrackArtistResponse? GetTrackArtistsByTrackId(int[] trackIds)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting Tidal GetTrackArtistsByTrackId for {trackIds.Length} tracks");
        using RestClient client = new RestClient(TracksUrl);

        return retryPolicy.Execute(() =>
        {
            RestRequest request = new RestRequest();
            request.AddHeader("Authorization", $"Bearer {this.AuthenticationResponse.AccessToken}");
            request.AddHeader("Accept", "application/vnd.api+json");
            request.AddHeader("Content-Type", "application/vnd.api+json");
            request.AddParameter("filter[id]", string.Join(',', trackIds));
            request.AddParameter("include", "artists");
            request.AddParameter("countryCode", _countryCode);
            
            return client.Get<TidalTrackArtistResponse>(request);
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