namespace MusicMover.Models.Tidal;

#pragma warning disable CS8618
public class TidalSearchTracksNextResponse
{
    public List<TidalAlbumItemsDataEntity>? Data { get; set; }
    public TidalRelationShipsAlbumsLinksEntity? Links { get; set; }
    public List<TidalSearchDataEntity>? Included { get; set; }
}