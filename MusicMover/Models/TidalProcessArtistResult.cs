using MusicMover.Models.Tidal;

namespace MusicMover.Models;

public class TidalProcessArtistResult
{
    public TidalSearchDataEntity? FoundAlbum { get; set; }
    public TidalSearchDataEntity? FoundTrack { get; set; }
    public List<string>? ArtistNames { get; set; }
    public TidalSearchResponse? AlbumTracks { get; set; }
    public bool Success { get; set; }
}