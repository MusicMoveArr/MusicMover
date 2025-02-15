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