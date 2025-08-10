using System.Diagnostics;
using System.Runtime.Caching;
using MusicMover.Models.Tidal;

namespace MusicMover.Services;

public class TidalAPICacheLayerService
{
    private readonly TidalAPIService _tidalAPIService;
    private readonly MemoryCache _cache;
    private const int ApiDelay = 4500;
    private const int SlidingCacheExpiration = 15;
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

    public async Task<TidalAuthenticationResponse?> AuthenticateAsync()
    {
        return await _tidalAPIService.AuthenticateAsync();
    }

    public async Task<TidalSearchResponse?> SearchResultsArtistsAsync(string searchTerm)
    {
        string cacheKey = $"SearchResultsArtists_{searchTerm}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalSearchResponse?)_cache.Get(cacheKey);
        }
        
        ApiDelaySleep();
        var result = await _tidalAPIService.SearchResultsArtistsAsync(searchTerm);
        AddToCache(cacheKey, result);
        return result;
    }

    public async Task<TidalSearchResponse?> SearchResultsTracksAsync(string searchTerm)
    {
        string cacheKey = $"SearchResultsTracks_{searchTerm}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalSearchResponse?)_cache.Get(cacheKey);
        }
        
        ApiDelaySleep();
        var result = await _tidalAPIService.SearchResultsTracksAsync(searchTerm);
        AddToCache(cacheKey, result);
        return result;
    }

    public async Task<TidalSearchResponse?> GetArtistInfoByIdAsync(int artistId)
    {
        string cacheKey = $"GetArtistInfoById_{artistId}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalSearchResponse?)_cache.Get(cacheKey);
        }
        
        ApiDelaySleep();
        var result = await _tidalAPIService.GetArtistInfoByIdAsync(artistId);
        AddToCache(cacheKey, result);
        return result;
    }

    public async Task<TidalSearchArtistNextResponse?> GetArtistNextInfoByIdAsync(int artistId, string next)
    {
        string cacheKey = $"GetArtistNextInfoById_{artistId}_{next}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalSearchArtistNextResponse?)_cache.Get(cacheKey);
        }
        ApiDelaySleep();
        var result = await _tidalAPIService.GetArtistNextInfoByIdAsync(artistId, next);
        AddToCache(cacheKey, result);
        return result;
    }

    public async Task<TidalSearchResponse?> GetTracksByAlbumIdAsync(int albumId)
    {
        string cacheKey = $"GetTracksByAlbumId_{albumId}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalSearchResponse?)_cache.Get(cacheKey);
        }
        ApiDelaySleep();
        var result = await _tidalAPIService.GetTracksByAlbumIdAsync(albumId);
        AddToCache(cacheKey, result);
        return result;
    }

    public async Task<TidalSearchTracksNextResponse?> GetTracksNextByAlbumIdAsync(int albumId, string next)
    {
        string cacheKey = $"GetTracksNextByAlbumId_{albumId}_{next}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalSearchTracksNextResponse?)_cache.Get(cacheKey);
        }
        ApiDelaySleep();
        var result = await _tidalAPIService.GetTracksNextByAlbumIdAsync(albumId, next);
        AddToCache(cacheKey, result);
        return result;
    }

    public async Task<TidalTrackArtistResponse?> GetTrackArtistsByTrackIdAsync(int[] trackIds)
    {
        string joinedTrackIds = string.Join(",", trackIds);
        string cacheKey = $"GetTrackArtistsByTrackId_{joinedTrackIds}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalTrackArtistResponse?)_cache.Get(cacheKey);
        }
        ApiDelaySleep();
        var result = await _tidalAPIService.GetTrackArtistsByTrackIdAsync(trackIds);
        AddToCache(cacheKey, result);
        return result;
    }

    public async Task<TidalSearchArtistNextResponse?> GetAlbumSelfInfoAsync(string selfLink)
    {
        string cacheKey = $"GetAlbumSelfInfo_{selfLink}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalSearchArtistNextResponse?)_cache.Get(cacheKey);
        }
        ApiDelaySleep();
        var result = await _tidalAPIService.GetAlbumSelfInfoAsync(selfLink);
        AddToCache(cacheKey, result);
        return result;
    }

    public async Task<TidalSearchTracksNextResponse?> GetTracksNextFromSearchAsync(string next)
    {
        string cacheKey = $"GetTracksNextFromSearch_{next}";

        if (_cache.Contains(cacheKey))
        {
            return (TidalSearchTracksNextResponse?)_cache.Get(cacheKey);
        }
        ApiDelaySleep();
        var result = await _tidalAPIService.GetTracksNextFromSearchAsync(next);
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