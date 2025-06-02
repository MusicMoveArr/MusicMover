using MusicMover.Models.MetadataAPI.Enums;

namespace MusicMover.Models.MetadataAPI.Entities;

#pragma warning disable CS8618
public class SearchArtistEntity
{
    public string Id { get; set; }
    public string Name { get; set; }
    public float Popularity { get; set; }
    public string Url { get; set; }
    public float TotalFollowers { get; set; }
    public string Genres { get; set; }
    
    public List<SearchArtistImageEntity>? Images { get; set; }
    public string ProviderType { get; set; }
    public DateTime LastSyncTime { get; set; }
}