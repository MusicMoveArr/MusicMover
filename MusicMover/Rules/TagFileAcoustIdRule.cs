using MusicMover.Rules.Machine;
using MusicMover.Services;

namespace MusicMover.Rules;

public class TagFileAcoustIdRule : Rule
{
    private readonly MusicBrainzService _musicBrainzService = new MusicBrainzService();

    public override ContinueType ContinueType { get; } = ContinueType.Continue;
    public override bool Required =>
        !string.IsNullOrWhiteSpace(StateObject.Options.AcoustIdApiKey) &&
        (StateObject.Options.AlwaysCheckAcoustId ||
         string.IsNullOrWhiteSpace(StateObject.MediaHandler.Artist) ||
         string.IsNullOrWhiteSpace(StateObject.MediaHandler.Album) ||
         string.IsNullOrWhiteSpace(StateObject.MediaHandler.AlbumArtist));


    public override async Task<StateResult> ExecuteAsync()
    {
        StateResult result = new StateResult();
        var match = await _musicBrainzService.GetMatchFromAcoustIdAsync(StateObject.MediaHandler,
            StateObject.Options.AcoustIdApiKey,
            StateObject.Options.SearchByTagNames,
            StateObject.Options.AcoustIdMatchPercentage,
            StateObject.Options.MusicBrainzMatchPercentage,
            StateObject.Options.AcoustIdMaxTimeSpan);
            
        bool success =
            match != null && await _musicBrainzService.WriteTagFromAcoustIdAsync(
                match,
                StateObject.MediaHandler, 
                StateObject.Options.OverwriteArtist, 
                StateObject.Options.OverwriteAlbum, 
                StateObject.Options.OverwriteTrack,
                StateObject.Options.OverwriteAlbumArtist);
        
        StateObject.MusicBrainzTaggingSuccess = success;

        result.Success = success;
        result.Message = success ? "Updated with AcoustId/MusicBrainz tags" : "AcoustId not found by Fingerprint";

        return result;
    }
}