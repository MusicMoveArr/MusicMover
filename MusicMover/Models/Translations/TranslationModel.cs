namespace MusicMover.Models.Translations;

public class TranslationModel
{
    public string ArtistName { get; set; }
    public string ArtistName_Translated { get; set; }
    public List<TranslationAlbumModel> Albums { get; set; }
}