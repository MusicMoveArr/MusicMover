using MusicMover.Rules.Machine;
using MusicMover.Services;

namespace MusicMover.Rules;

public class TagFileMetadataApiRule : Rule
{
    private static MiniMediaMetadataService _miniMediaMetadataService;
    private static TranslationService _translationService;

    public override ContinueType ContinueType { get; } = ContinueType.Continue;
    public override bool Required =>
        !string.IsNullOrWhiteSpace(StateObject.Options.MetadataApiBaseUrl);

    public override async Task<StateResult> ExecuteAsync()
    {
        if (_miniMediaMetadataService == null)
        {
            _translationService = new TranslationService(StateObject.Options.TranslationPath);
            await _translationService.LoadTranslationsAsync();
            _miniMediaMetadataService = new MiniMediaMetadataService(StateObject.Options.MetadataApiBaseUrl, StateObject.Options.MetadataApiProviders, _translationService);
        }
        
        StateResult result = new StateResult();
        var matches = await _miniMediaMetadataService.GetMatchesAsync(
            StateObject.MediaHandler,
            StateObject.Options.MetadataApiMatchPercentage);

        bool success = false;
        foreach (var match in matches)
        {
            if (await _miniMediaMetadataService.WriteTagsToFileAsync(
                    match,
                    StateObject.MediaHandler,
                    StateObject.Options.OverwriteArtist,
                    StateObject.Options.OverwriteAlbum,
                    StateObject.Options.OverwriteTrack,
                    StateObject.Options.OverwriteAlbumArtist))
            {
                success = true;
            }
        }
            
        StateObject.MetadataApiTaggingSuccess = success;
        result.Success = success;
        result.Message = success ? "Updated with MetadataAPI tags" : "MetadataAPI track not found for";

        return result;
    }
}