using Newtonsoft.Json.Linq;
using RestSharp;

namespace MusicMover.Services;

public class AcoustIdService
{
    public JObject? LookupAcoustId(string acoustIdApiKey, string fingerprint, int duration)
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

        var response = client.Execute(request);

        if (response.IsSuccessful)
        {
            var content = response.Content;

            if (!string.IsNullOrWhiteSpace(content))
            {
                JObject jsonResponse = JObject.Parse(content);
                
                return jsonResponse;
            }
        }
        else
        {
            Console.WriteLine("Error: " + response.ErrorMessage);
        }
        return null;
    }
}