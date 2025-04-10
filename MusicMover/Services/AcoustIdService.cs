using MusicMover.Models;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace MusicMover.Services;

public class AcoustIdService
{
    public AcoustIdResponse? LookupAcoustId(string acoustIdApiKey, string fingerprint, int duration)
    {
        if (string.IsNullOrWhiteSpace(acoustIdApiKey))
        {
            return null;
        }
        
        var client = new RestClient("https://api.acoustid.org/v2/lookup");
        var request = new RestRequest();
        request.AddParameter("client", acoustIdApiKey);
        request.AddParameter("meta", "recordings");
        request.AddParameter("duration", duration);
        request.AddParameter("fingerprint", fingerprint);

        return client.Get<AcoustIdResponse>(request);
    }
    
    public AcoustIdResponse? LookupByAcoustId(string acoustIdApiKey, string acoustId)
    {
        if (string.IsNullOrWhiteSpace(acoustIdApiKey))
        {
            return null;
        }
        
        var client = new RestClient("https://api.acoustid.org/v2/lookup");
        var request = new RestRequest();
        request.AddParameter("client", acoustIdApiKey);
        request.AddParameter("meta", "recordings");
        request.AddParameter("trackid", acoustId);

        return client.Get<AcoustIdResponse>(request);
    }
}