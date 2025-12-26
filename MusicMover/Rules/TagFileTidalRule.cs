using MusicMover.Rules.Machine;
using MusicMover.Services;

namespace MusicMover.Rules;

public class TagFileTidalRule : Rule
{
    private static TidalService _tidalService;

    public override ContinueType ContinueType { get; } = ContinueType.Continue;
    public override bool Required =>
        !StateObject.MetadataApiTaggingSuccess &&
        !string.IsNullOrWhiteSpace(StateObject.Options.TidalClientId) &&
        !string.IsNullOrWhiteSpace(StateObject.Options.TidalClientSecret) &&
        !string.IsNullOrWhiteSpace(StateObject.Options.TidalCountryCode);

    public override async Task<StateResult> ExecuteAsync()
    {
        if (_tidalService == null)
        {
            _tidalService = new TidalService(StateObject.Options.TidalClientId, StateObject.Options.TidalClientSecret, StateObject.Options.TidalCountryCode);
        }
        
        StateResult result = new StateResult();
        bool success = await _tidalService.WriteTagsAsync(StateObject.MediaHandler, 
            StateObject.Options.OverwriteArtist, 
            StateObject.Options.OverwriteAlbum, 
            StateObject.Options.OverwriteTrack,
            StateObject.Options.OverwriteAlbumArtist,
            StateObject.Options.TidalMatchPercentage);
            
        StateObject.TidalTaggingSuccess = success;
        result.Success = success;
        result.Message = success ? "Updated with Tidal tags" : "Tidal record not found by MediaTag information";

        return result;
    }
}