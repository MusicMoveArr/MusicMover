using System.Diagnostics;
using MusicMover.Helpers;
using MusicMover.Models.AcoustId;
using Polly;
using Polly.Retry;
using RestSharp;

namespace MusicMover.Services;

public class AcoustIdService
{
    public async Task<AcoustIdResponse?> LookupAcoustIdAsync(string acoustIdApiKey, string fingerprint, int duration)
    {
        if (string.IsNullOrWhiteSpace(acoustIdApiKey))
        {
            return null;
        }
        
        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        var client = new RestClient("https://api.acoustid.org/v2/lookup");

        return await retryPolicy.ExecuteAsync(async () =>
        {
            var request = new RestRequest();
            request.AddParameter("client", acoustIdApiKey);
            request.AddParameter("meta", "recordings");
            request.AddParameter("duration", duration);
            request.AddParameter("fingerprint", fingerprint);

            return await client.GetAsync<AcoustIdResponse>(request);
        });
    }
    
    public async Task<AcoustIdResponse?> LookupByAcoustIdAsync(string acoustIdApiKey, string acoustId)
    {
        if (string.IsNullOrWhiteSpace(acoustIdApiKey))
        {
            return null;
        }
        
        AsyncRetryPolicy retryPolicy = GetRetryPolicy();
        var client = new RestClient("https://api.acoustid.org/v2/lookup");
        return await retryPolicy.ExecuteAsync(async () =>
        {
            var request = new RestRequest();
            request.AddParameter("client", acoustIdApiKey);
            request.AddParameter("meta", "recordings");
            request.AddParameter("trackid", acoustId);
            
            return await client.GetAsync<AcoustIdResponse>(request);
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