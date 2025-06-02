using MusicMover.Models.MetadataAPI.Enums;

namespace MusicMover.Models.MetadataAPI;

#pragma warning disable CS8618
public class SearchAlbumRequest
{
    public string Provider { get; set; }
    public string AlbumId { get; set; }
    public string ArtistId { get; set; }
    public string AlbumName { get; set; }
    public int Offset { get; set; }
}