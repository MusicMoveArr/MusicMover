using MusicMover.Models.MetadataAPI.Enums;

namespace MusicMover.Models.MetadataAPI;

#pragma warning disable CS8618
public class SearchRequestModel
{
    public string Provider { get; set; }
    
    public string ArtistName { get; set; }
    public string AlbumName { get; set; }
    public string TrackName { get; set; }
    public string ISRC { get; set; }
    public string UPC { get; set; }
}