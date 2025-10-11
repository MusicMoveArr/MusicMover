using System.Runtime.Caching;
using MusicMover.Models.MetadataAPI;

namespace MusicMover.Services;

public class MiniMediaMetadataApiCacheLayerService
{
    private readonly MiniMediaMetadataApiService _miniMediaMetadataApiService;
    private readonly MemoryCache _cache;
    private const int SlidingCacheExpiration = 15;

    public MiniMediaMetadataApiCacheLayerService(string baseUrl, List<string> providerTypes)
    {
        _miniMediaMetadataApiService = new MiniMediaMetadataApiService(baseUrl, providerTypes);
        _cache = MemoryCache.Default;
    }
    
    private void AddToCache(string key, object? value)
    {
        if (value != null)
        {
            CacheItemPolicy policy = new CacheItemPolicy();
            policy.SlidingExpiration = TimeSpan.FromMinutes(SlidingCacheExpiration);
            _cache.Add(key, value, policy);
        }
    }
    
    public async Task<SearchArtistResponse?> SearchArtistsAsync(string searchTerm)
    {
        string cacheKey = $"SearchArtistsAsync_{searchTerm}";

        if (_cache.Contains(cacheKey))
        {
            return (SearchArtistResponse?)_cache.Get(cacheKey);
        }
        
        var result = await _miniMediaMetadataApiService.SearchArtistsAsync(searchTerm);
        AddToCache(cacheKey, result);
        return result;
    }
    
    public async Task<SearchArtistResponse?> GetArtistByIdAsync(string artistId, string providerType)
    {
        string cacheKey = $"GetArtistByIdAsync_{artistId}_{providerType}";

        if (_cache.Contains(cacheKey))
        {
            return (SearchArtistResponse?)_cache.Get(cacheKey);
        }
        
        var result = await _miniMediaMetadataApiService.GetArtistByIdAsync(artistId, providerType);
        AddToCache(cacheKey, result);
        return result;
    }
    
    public async Task<SearchTrackResponse?> SearchTracksAsync(string searchTerm, string artistId, string providerType)
    {
        string cacheKey = $"SearchTracksAsync_{searchTerm}_{artistId}_{providerType}";

        if (_cache.Contains(cacheKey))
        {
            return (SearchTrackResponse?)_cache.Get(cacheKey);
        }
        
        var result = await _miniMediaMetadataApiService.SearchTracksAsync(searchTerm, artistId, providerType);
        AddToCache(cacheKey, result);
        return result;
    }
    
    public async Task<SearchTrackResponse?> GetTrackByIdAsync(string trackId, string providerType)
    {
        string cacheKey = $"GetTrackByIdAsync{trackId}_{providerType}";

        if (_cache.Contains(cacheKey))
        {
            return (SearchTrackResponse?)_cache.Get(cacheKey);
        }
        
        var result = await _miniMediaMetadataApiService.GetTrackByIdAsync(trackId, providerType);
        AddToCache(cacheKey, result);
        return result;
    }
}