using MusicMover.Models;
using RestSharp;

namespace MusicMover.Services;

public class MusicBrainzAPIService
{
    public MusicBrainzArtistModel GetRecordingById(string recordingId)
    {
        Console.WriteLine($"Requesting MusicBrainz GetRecordingById, {recordingId}");

        try
        {
            string url = $"https://musicbrainz.org/ws/2/recording/{recordingId}?fmt=json&inc=isrcs+artists+releases+release-groups+url-rels+media";
            var options = new RestClientOptions();
            using RestClient client = new RestClient(url);
            RestRequest request = new RestRequest();
            
            var response = client.Get<MusicBrainzArtistModel>(request);
            return response;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        return null;
    }
}