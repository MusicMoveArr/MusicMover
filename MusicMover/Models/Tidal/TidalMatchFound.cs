namespace MusicMover.Models.Tidal;

public class TidalMatchFound
{
    public TidalSearchDataEntity FoundTrack { get; set; }
    public TidalSearchResponse AlbumTracks { get; set; }
    public TidalSearchDataEntity Album { get; set; }
    public List<string> ArtistNames { get; set; }

    public TidalMatchFound(
        TidalSearchDataEntity foundTrack,
        TidalSearchResponse albumTracks,
        TidalSearchDataEntity album,
        List<string> artistNames)
    {
        this.FoundTrack = foundTrack;
        this.AlbumTracks = albumTracks;
        this.Album = album;
        this.ArtistNames = artistNames;
    }
}