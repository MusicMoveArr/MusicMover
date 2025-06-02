using MusicMover.Models.MetadataAPI.Enums;

namespace MusicMover.Models.MetadataAPI.Entities;

#pragma warning disable CS8618
public class SearchAlbumEntity
{
    public string Id { get; set; }
    public string ArtistId { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string ReleaseDate { get; set; }
    public int TotalTracks { get; set; }
    public string? Url { get; set; }
    public string? Copyright { get; set; }
    public string? Label { get; set; }
    public float Popularity { get; set; }
    public string? UPC { get; set; }
    public List<SearchAlbumArtistEntity>? Artists { get; set; }
    public List<SearchAlbumImageEntity>? Images { get; set; }
    public string ProviderType { get; set; }
}