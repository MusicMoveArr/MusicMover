using MusicMover.Models.MetadataAPI.Enums;
using MusicMover.Models.MetadataAPI.Entities;

namespace MusicMover.Models.MetadataAPI;

public class SearchTrackResponse
{
    public string SearchResult { get; set; }
    public List<SearchTrackEntity> Tracks { get; set; }
}