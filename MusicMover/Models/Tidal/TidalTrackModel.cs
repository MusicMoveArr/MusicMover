namespace MusicMover.Models.Tidal;

public class TidalTrackModel
{
    public string TrackName { get; set; }
    public int TrackId { get; set; }
    public int AlbumId { get; set; }
    public string AlbumName { get; set; }
    public int DiscNumber { get; set; }
    public string Duration { get; set; }
    public bool Explicit { get; set; }
    public string TrackHref { get; set; }
    public string AlbumHref { get; set; }
    public int TrackNumber { get; set; }
    public string ReleaseDate { get; set; }
    public int TotalTracks { get; set; }
    public string Copyright { get; set; }
    public string ArtistHref { get; set; }
    public string ArtistName { get; set; }
    public int ArtistId { get; set; }
    public string TrackISRC { get; set; }
    public string AlbumUPC { get; set; }
}