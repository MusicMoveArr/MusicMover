using MusicMover.Models.MetadataAPI.Enums;
using MusicMover.Models.MetadataAPI.Entities;

namespace MusicMover.Models.MetadataAPI;

public class SearchAlbumResponse
{
    public string SearchResult { get; set; }
    public List<SearchAlbumEntity> Albums { get; set; }
}