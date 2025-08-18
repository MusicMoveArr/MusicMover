using System.Diagnostics;
using MusicMover.Helpers;
using MusicMover.Models;
using MusicMover.Models.MusicBrainz;
using Polly;
using Polly.Retry;
using RestSharp;
using Spectre.Console;

namespace MusicMover.Services;

public class MusicBrainzAPIService
{
    private static Stopwatch _apiStopwatch = Stopwatch.StartNew();
    
    public async Task<MusicBrainzArtistModel?> GetRecordingByIdAsync(string recordingId)
    {
        Delay();
        
        Logger.WriteLine($"Requesting MusicBrainz GetRecordingById, {recordingId}", true);

        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        
        string url = $"https://musicbrainz.org/ws/2/recording/{recordingId}?fmt=json&inc=isrcs+artists+releases+release-groups+url-rels+media";
        return await retryPolicy.ExecuteAsync(async () =>
        {
            using RestClient client = new RestClient(url);
            RestRequest request = new RestRequest();
        
            var response = await client.GetAsync<MusicBrainzArtistModel>(request);
            return response;
        });
    }
    public async Task<MusicBrainzArtistReleaseModel?> GetReleaseWithLabelAsync(string musicBrainzReleaseId)
    {
        Delay();

        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        //ServiceUnavailable
        
        Logger.WriteLine($"Requesting MusicBrainz GetReleaseWithLabel '{musicBrainzReleaseId}'", true);
        string url = $"https://musicbrainz.org/ws/2/release/{musicBrainzReleaseId}?inc=labels&fmt=json";

        return await retryPolicy.ExecuteAsync(async () =>
        {
            using RestClient client = new RestClient(url);
            RestRequest request = new RestRequest();
            var response = await client.GetAsync<MusicBrainzArtistReleaseModel>(request);
            
            return response;
        });
    }
    public async Task<MusicBrainzArtistReleaseModel?> GetReleaseWithAllAsync(string musicBrainzReleaseId)
    {
        Delay();

        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        //ServiceUnavailable
        
        Logger.WriteLine($"Requesting MusicBrainz GetReleaseWithAll '{musicBrainzReleaseId}'", true);
        string url = $"https://musicbrainz.org/ws/2/release/{musicBrainzReleaseId}?inc=artists+release-groups+url-rels+media+recordings&fmt=json";

        return await retryPolicy.ExecuteAsync(async () =>
        {
            using RestClient client = new RestClient(url);
            RestRequest request = new RestRequest();
            var response = await client.GetAsync<MusicBrainzArtistReleaseModel>(request);
            
            return response;
        });
    }
    public async Task<MusicBrainzArtistInfoModel?> GetArtistInfoAsync(string musicBrainzArtistId)
    {
        Delay();

        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting MusicBrainz GetArtistInfo '{musicBrainzArtistId}'");
        string url = $"https://musicbrainz.org/ws/2/artist/{musicBrainzArtistId}?inc=aliases&fmt=json";
        using RestClient client = new RestClient(url);

        return await retryPolicy.ExecuteAsync(async () =>
        {
            RestRequest request = new RestRequest();
            return await client.GetAsync<MusicBrainzArtistInfoModel>(request);
        });
    }
    public async Task<MusicBrainzRecordingQueryModel?> SearchReleaseAsync(string artist, string album, string trackname)
    {
        Delay();
        
        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting MusicBrainz Recording lookup artist:'{artist}', trackname:'{trackname}'");
        string url = $"https://musicbrainz.org/ws/2/recording?fmt=json&inc=isrcs+artists+releases+release-groups+url-rels+media+recordings&query=track:\"{trackname}\" AND artist:\"{artist}\"";
        using RestClient client = new RestClient(url);

        return await retryPolicy.ExecuteAsync(async () =>
        {
            RestRequest request = new RestRequest();
            return await client.GetAsync<MusicBrainzRecordingQueryModel>(request);
        });
    }

    private static void Delay()
    {
        lock (_apiStopwatch)
        {
            int elapsed = (int)_apiStopwatch.ElapsedMilliseconds;
            if (elapsed < 1000)
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
                _apiStopwatch.Restart();
            }
        }
    }
    
    private AsyncRetryPolicy GetRetryPolicy()
    {
        AsyncRetryPolicy retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(5, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) => {
                    Logger.WriteLine($"Retry {retryCount} after {timeSpan.TotalSeconds} sec due to: {exception.Message}");
                });
        return retryPolicy;
    }
}