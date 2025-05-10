using MusicMover.Models.MetadataAPI.Enums;
using MusicMover.Models.MetadataAPI.Entities;

namespace MusicMover.Models.MetadataAPI;

public class SearchArtistResponse
{
    public string SearchResult { get; set; }
    public List<SearchArtistEntity> Artists { get; set; }
}