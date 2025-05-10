using MusicMover.Models.MetadataAPI.Enums;

namespace MusicMover.Models.MetadataAPI;

public class SearchArtistRequest
{
    public string Provider { get; set; }
    public string Id { get; set; }
    public string Name { get; set; }
    public int Offset { get; set; }
}