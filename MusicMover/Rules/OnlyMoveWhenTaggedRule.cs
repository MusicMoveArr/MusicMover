using MusicMover.Rules.Machine;

namespace MusicMover.Rules;

public class OnlyMoveWhenTaggedRule : Rule
{
    public override bool Required =>
        StateObject.Options.OnlyMoveWhenTagged &&
        !StateObject.MusicBrainzTaggingSuccess &&
        !StateObject.TidalTaggingSuccess &&
        !StateObject.MetadataApiTaggingSuccess &&
        StateObject.Options.TrustAcoustIdWhenTaggingFailed &&
        !string.IsNullOrWhiteSpace(StateObject.Options.AcoustIdApiKey);
    
    public override ContinueType ContinueType { get; } = ContinueType.Continue;
    public override async Task<StateResult> ExecuteAsync()
    {
        //empty the tags we use for tagging and try again
        StateObject.MediaHandler.SetMediaTagValue(string.Empty, "Album");
        StateObject.MediaHandler.SetMediaTagValue(string.Empty, "AlbumArtist");
        StateObject.MediaHandler.SetMediaTagValue(string.Empty, "Artist");
        StateObject.MediaHandler.SetMediaTagValue(string.Empty, "Title");
        
        SimpleRuleEngine ruleEngine = new SimpleRuleEngine();
        ruleEngine.AddRule<TagFileAcoustIdRule>();
        ruleEngine.AddRule<TagFileMetadataApiRule>();
        ruleEngine.AddRule<TagFileTidalRule>();
        var results = await ruleEngine.RunAsync(StateObject);

        return new StateResult(results.Any(r => r.Success));
    }
}