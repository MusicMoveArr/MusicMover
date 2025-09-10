namespace MusicMover.Models.Tidal;

#pragma warning disable CS8618
public class TidalAttributeEntity
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? BarcodeId { get; set; }
    public int NumberOfVolumes { get; set; }
    public int NumberOfItems { get; set; }
    public string? Duration { get; set; }
    public bool Explicit { get; set; }
    public string? ReleaseDate { get; set; }
    public TidalAttributeTextEntity? Copyright { get; set; }
    public float Popularity { get; set; }
    public List<string> Availability { get; set; }
    public List<string> MediaTags { get; set; }
    public string? Type { get; set; }
    public string? ISRC { get; set; }
    
    public List<TidalImageLinkEntity> ImageLinks { get; set; }
    public List<TidalExternalLinkEntity> ExternalLinks { get; set; }

    public string FullTrackName
    {
        get
        {
            string _version = !string.IsNullOrWhiteSpace(Version) ? $" ({Version})" : string.Empty;
            return $"{Title}{_version}";
        }
    }
}