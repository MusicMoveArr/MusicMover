using MusicMover.Helpers;
using MusicMover.MediaHandlers;
using MusicMover.Models.Translations;
using Newtonsoft.Json;

namespace MusicMover.Services;

public class TranslationService
{
    private readonly List<TranslationModel> _translations;
    private readonly string _directoryPath;
    public TranslationService(string directoryPath)
    {
        _directoryPath =  directoryPath;
        _translations = new List<TranslationModel>();
    }

    public async Task LoadTranslationsAsync()
    {
        _translations.Clear();
        foreach (string filePath in Directory.EnumerateFiles(_directoryPath, "*.json"))
        {
            try
            {
                var translation = JsonConvert.DeserializeObject<List<TranslationModel>>(File.ReadAllText(filePath));
                
                _translations.AddRange(translation);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + "\r\n" + e.StackTrace);
            }
        }
    }

    public TranslationResult Translate(MediaHandler mediaHandler)
    {
        var artistTranslation = _translations
            .FirstOrDefault(t =>
                mediaHandler.AllArtistNames.Any(artist =>
                    string.Equals(t.ArtistName, artist, StringComparison.OrdinalIgnoreCase)));
        
        var albumTranslation = artistTranslation?.Albums
            .FirstOrDefault(t =>
                string.Equals(t.AlbumName, mediaHandler.Album, StringComparison.OrdinalIgnoreCase));

        var trackTranslation = albumTranslation?.Tracks
            .FirstOrDefault(t =>
                string.Equals(t.TrackName, mediaHandler.Title, StringComparison.OrdinalIgnoreCase));


        if (artistTranslation == null)
        {
            return null;
        }

        TranslationResult result = new TranslationResult();
        result.ArtistName = artistTranslation.ArtistName_Translated;
        result.AlbumName = !string.IsNullOrWhiteSpace(albumTranslation?.AlbumName_Translated) ? albumTranslation.AlbumName_Translated : mediaHandler.Album;
        result.TrackName = !string.IsNullOrWhiteSpace(trackTranslation?.TrackName_Translated) ? trackTranslation?.TrackName_Translated : mediaHandler.Title;
        
        return result;
    }
}