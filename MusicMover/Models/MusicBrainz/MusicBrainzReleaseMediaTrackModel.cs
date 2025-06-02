using System.Text.Json.Serialization;

namespace MusicMover.Models.MusicBrainz;

public class MusicBrainzReleaseMediaTrackModel
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public int? Length { get; set; }
    
    [JsonConverter(typeof(MusicBrainzReleaseMediaTrackModelJsonConverter))]
    public int? Number { get; set; }
    
    public int? Position { get; set; }
    public MusicBrainzReleaseMediaTrackRecordingModel? Recording { get; set; }
}