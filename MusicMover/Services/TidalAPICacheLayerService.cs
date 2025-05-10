using System.Diagnostics;
using System.Runtime.Caching;
using MusicMover.Models.Tidal;

namespace MusicMover.Services;

public class TidalAPICacheLayerService
{
    private readonly TidalAPIService _tidalAPIService;
    private readonly MemoryCache _cache;
    private const int ApiDelay = 4500;
    private const int SlidingCacheExpiration = 120;
    private Stopwatch _apiStopwatch = Stopwatch.StartNew();
    
    public TidalAPICacheLayerService(string clientId, string clientSecret, string countryCode)
    {
        _tidalAPIService = new TidalAPIService(clientId, clientSecret, countryCode);
        _cache = MemoryCache.Default;
    }
    
    public TidalAuthenticationResponse? AuthenticationResponse { get => _tidalAPIService.AuthenticationResponse; }

    private void AddToCache(string key, object? value)
    {
        if (value != null)
        {
            CacheItemPolicy policy = new CacheItemPolicy();
            policy.SlidingExpiration = TimeSpan.FromMinutes(SlidingCacheExpiration);
            _cache.Add(key, value, policy);
        }
    }

    public TidalAuthenticationResponse? Authenticate()
    {
        return _tidalAPIService.Authenticate();
    }

    public TidalSearchResponse? SearchResultsArtists(string searchTerm)
    {
        string cacheKey = $"SearchResultsArtists_{searchTerm}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalSearchResponse?)_cache.Get(cacheKey);
        }
        
        ApiDelaySleep();
        var result = _tidalAPIService.SearchResultsArtists(searchTerm);
        AddToCache(cacheKey, result);
        return result;
    }

    public TidalSearchResponse? SearchResultsTracks(string searchTerm)
    {
        string cacheKey = $"SearchResultsTracks_{searchTerm}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalSearchResponse?)_cache.Get(cacheKey);
        }
        
        ApiDelaySleep();
        var result = _tidalAPIService.SearchResultsTracks(searchTerm);
        AddToCache(cacheKey, result);
        return result;
    }

    public TidalSearchResponse? GetArtistInfoById(int artistId)
    {
        string cacheKey = $"GetArtistInfoById_{artistId}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalSearchResponse?)_cache.Get(cacheKey);
        }
        
        ApiDelaySleep();
        var result = _tidalAPIService.GetArtistInfoById(artistId);
        AddToCache(cacheKey, result);
        return result;
    }

    public TidalSearchArtistNextResponse? GetArtistNextInfoById(int artistId, string next)
    {
        string cacheKey = $"GetArtistNextInfoById_{artistId}_{next}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalSearchArtistNextResponse?)_cache.Get(cacheKey);
        }
        ApiDelaySleep();
        var result = _tidalAPIService.GetArtistNextInfoById(artistId, next);
        AddToCache(cacheKey, result);
        return result;
    }

    public TidalSearchResponse? GetTracksByAlbumId(int albumId)
    {
        string cacheKey = $"GetTracksByAlbumId_{albumId}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalSearchResponse?)_cache.Get(cacheKey);
        }
        ApiDelaySleep();
        var result = _tidalAPIService.GetTracksByAlbumId(albumId);
        AddToCache(cacheKey, result);
        return result;
    }

    public TidalSearchTracksNextResponse? GetTracksNextByAlbumId(int albumId, string next)
    {
        string cacheKey = $"GetTracksNextByAlbumId_{albumId}_{next}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalSearchTracksNextResponse?)_cache.Get(cacheKey);
        }
        ApiDelaySleep();
        var result = _tidalAPIService.GetTracksNextByAlbumId(albumId, next);
        AddToCache(cacheKey, result);
        return result;
    }

    public TidalTrackArtistResponse? GetTrackArtistsByTrackId(int[] trackIds)
    {
        string joinedTrackIds = string.Join(",", trackIds);
        string cacheKey = $"GetTrackArtistsByTrackId_{joinedTrackIds}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalTrackArtistResponse?)_cache.Get(cacheKey);
        }
        ApiDelaySleep();
        var result = _tidalAPIService.GetTrackArtistsByTrackId(trackIds);
        AddToCache(cacheKey, result);
        return result;
    }

    public TidalSearchArtistNextResponse? GetAlbumSelfInfo(string selfLink)
    {
        string cacheKey = $"GetAlbumSelfInfo_{selfLink}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalSearchArtistNextResponse?)_cache.Get(cacheKey);
        }
        ApiDelaySleep();
        var result = _tidalAPIService.GetAlbumSelfInfo(selfLink);
        AddToCache(cacheKey, result);
        return result;
    }

    public TidalSearchTracksNextResponse? GetTracksNextFromSearch(string next)
    {
        string cacheKey = $"GetTracksNextFromSearch_{next}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalSearchTracksNextResponse?)_cache.Get(cacheKey);
        }
        ApiDelaySleep();
        var result = _tidalAPIService.GetTracksNextFromSearch(next);
        AddToCache(cacheKey, result);
        return result;
    }

    private void ApiDelaySleep()
    {
        if (_apiStopwatch.ElapsedMilliseconds < ApiDelay)
        {
            Thread.Sleep(ApiDelay);
        }
        _apiStopwatch.Restart();
    }
}