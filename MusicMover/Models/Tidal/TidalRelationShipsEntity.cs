namespace MusicMover.Models.Tidal;

#pragma warning disable CS8618
public class TidalRelationShipsEntity
{
    public TidalAlbumItemsEntity Items { get; set; }
    public TidalRelationShipsAlbumsEntity Albums { get; set; }
    public TidalRelationShipsArtistsEntity Artists { get; set; }
    public TidalRelationShipsTracksEntity Tracks { get; set; }
}