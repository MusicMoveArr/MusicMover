using System.ComponentModel.DataAnnotations;
using MusicMover.Models.MetadataAPI.Enums;

namespace MusicMover.Models.MetadataAPI.Entities;

#pragma warning disable CS8618
public class SearchTrackEntity
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int DiscNumber { get; set; }
    public int TrackNumber { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Explicit { get; set; }
    public string? ISRC { get; set; }
    public string Label { get; set; }
    public string Copyright { get; set; }
    public string Availability { get; set; }
    public string MediaTags { get; set; }
    public float Popularity { get; set; }
    public string Url { get; set; }
    public List<SearchTrackArtistEntity>? Artists { get; set; }
    public List<SearchTrackImageEntity>? Images { get; set; }
    public SearchTrackAlbumEntity  Album { get; set; }
    public SearchTrackMusicBrainzEntity  MusicBrainz { get; set; }
    
    public string ProviderType { get; set; }
}