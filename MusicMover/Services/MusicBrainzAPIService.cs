using System.Diagnostics;
using MusicMover.Models;
using Polly;
using Polly.Retry;
using RestSharp;

namespace MusicMover.Services;

public class MusicBrainzAPIService
{
    public MusicBrainzArtistModel? GetRecordingById(string recordingId)
    {
        Console.WriteLine($"Requesting MusicBrainz GetRecordingById, {recordingId}");

        RetryPolicy retryPolicy = GetRetryPolicy();
        
        string url = $"https://musicbrainz.org/ws/2/recording/{recordingId}?fmt=json&inc=isrcs+artists+releases+release-groups+url-rels+media";
        return retryPolicy.Execute(() =>
        {
            using RestClient client = new RestClient(url);
            RestRequest request = new RestRequest();
        
            var response = client.Get<MusicBrainzArtistModel>(request);
            return response;
        });
    }
    public MusicBrainzArtistReleaseModel? GetReleaseWithLabel(string musicBrainzReleaseId)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        //ServiceUnavailable
        
        Console.WriteLine($"Requesting MusicBrainz GetReleaseWithLabel '{musicBrainzReleaseId}'");
        string url = $"https://musicbrainz.org/ws/2/release/{musicBrainzReleaseId}?inc=labels&fmt=json";

        return retryPolicy.Execute(() =>
        {
            using RestClient client = new RestClient(url);
            RestRequest request = new RestRequest();
            var response =  client.Get<MusicBrainzArtistReleaseModel>(request);
            
            return response;
        });
    }
    public MusicBrainzArtistReleaseModel? GetReleaseWithAll(string musicBrainzReleaseId)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        //ServiceUnavailable
        
        Console.WriteLine($"Requesting MusicBrainz GetReleaseWithAll '{musicBrainzReleaseId}'");
        string url = $"https://musicbrainz.org/ws/2/release/{musicBrainzReleaseId}?inc=artists+release-groups+url-rels+media+recordings&fmt=json";

        return retryPolicy.Execute(() =>
        {
            using RestClient client = new RestClient(url);
            RestRequest request = new RestRequest();
            var response =  client.Get<MusicBrainzArtistReleaseModel>(request);
            
            return response;
        });
    }
    public MusicBrainzArtistInfoModel? GetArtistInfo(string musicBrainzArtistId)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting MusicBrainz GetArtistInfo '{musicBrainzArtistId}'");
        string url = $"https://musicbrainz.org/ws/2/artist/{musicBrainzArtistId}?inc=aliases&fmt=json";
        using RestClient client = new RestClient(url);

        return retryPolicy.Execute(() =>
        {
            RestRequest request = new RestRequest();
            return client.Get<MusicBrainzArtistInfoModel>(request);
        });
    }
    public MusicBrainzRecordingQueryModel? SearchRelease(string artist, string album, string trackname)
    {
        RetryPolicy retryPolicy = GetRetryPolicy();
        Debug.WriteLine($"Requesting MusicBrainz Recording lookup artist:'{artist}', album:'{album}', trackname:'{trackname}'");
        string url = $"https://musicbrainz.org/ws/2/recording?fmt=json&inc=isrcs+artists+releases+release-groups+url-rels+media+recordings&query=track:\"{trackname}\" AND artist:\"{artist}\"";
        using RestClient client = new RestClient(url);

        return retryPolicy.Execute(() =>
        {
            RestRequest request = new RestRequest();
            return client.Get<MusicBrainzRecordingQueryModel>(request);
        });
    }
    
    private RetryPolicy GetRetryPolicy()
    {
        RetryPolicy retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetry(5, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) => {
                    Console.WriteLine($"Retry {retryCount} after {timeSpan.TotalSeconds} sec due to: {exception.Message}");
                });
        return retryPolicy;
    }
}