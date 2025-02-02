using System.Text.Json;
using System.Text.Json.Serialization;
namespace MusicMover;

public class MusicBrainzReleaseMediaTrackModelJsonConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string value = reader.GetString();
        int.TryParse(value, out int number);
        return number;
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        
    }
}