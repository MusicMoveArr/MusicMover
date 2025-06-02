namespace MusicMover.Models.Tidal;

#pragma warning disable CS8618
public class TidalTrackArtistIncludedEntity
{
    public string Id { get; set; }
    public string Type { get; set; }
    public TidalAttributeEntity Attributes { get; set; }
    public TidalRelationShipsEntity RelationShips { get; set; }
}