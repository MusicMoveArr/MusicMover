namespace MusicMover.Models.Tidal;

#pragma warning disable CS8618
public class TidalSearchResponse
{
    public TidalSearchDataEntity? Data { get; set; }
    
    public List<TidalSearchDataEntity>? Included { get; set; }
}