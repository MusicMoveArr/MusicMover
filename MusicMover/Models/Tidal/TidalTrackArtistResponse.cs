namespace MusicMover.Models.Tidal;

#pragma warning disable CS8618
public class TidalTrackArtistResponse
{
    public List<TidalTrackArtistEntityResponse> Data { get; set; }
    public List<TidalTrackArtistIncludedEntity> Included { get; set; }
}