namespace MusicMover.Models.Translations;

public class TranslationAlbumModel
{
    public string AlbumName { get; set; }
    public string AlbumName_Translated { get; set; }
    public List<TranslationTrackModel> Tracks { get; set; }
}