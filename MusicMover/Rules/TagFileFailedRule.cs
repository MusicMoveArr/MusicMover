using System.Text.RegularExpressions;
using MusicMover.Rules.Machine;

namespace MusicMover.Rules;

public class TagFileFailedRule : Rule
{
    public override ContinueType ContinueType { get; } = ContinueType.Continue;
    public override bool Required =>
        !StateObject.MusicBrainzTaggingSuccess &&
        !StateObject.TidalTaggingSuccess &&
        !StateObject.MetadataApiTaggingSuccess;

    public override async Task<StateResult> ExecuteAsync()
    {
        StateResult result = new StateResult();
        string oldArtist = StateObject.MediaHandler.Artist ?? string.Empty;
        string oldTitle = StateObject.MediaHandler.Title ?? string.Empty;
            
        //remove numbers at the start of the title
        StateObject.MediaHandler.SetMediaTagValue("Title", Regex.Replace(StateObject.MediaHandler.Title, @"^[0-9.\- ]*", string.Empty).TrimStart());
        //remove (Album Version)
        StateObject.MediaHandler.SetMediaTagValue("Title", StateObject.MediaHandler.Title.Replace("(Album Version)", string.Empty, StringComparison.OrdinalIgnoreCase));
            
        //remove the artist name at the start of the title
        //if artist name is empty, fill artist name partly from the title
        var artistMatches = Regex.Matches(StateObject.MediaHandler.Title, @"^([\d\w ]{3,})\-");
        if (artistMatches.Count > 0)
        {
            StateObject.MediaHandler.SetMediaTagValue("Title", Regex.Replace(StateObject.MediaHandler.Title, @"^([\d\w ]{3,})\-", string.Empty).TrimStart());
                
            if (string.IsNullOrWhiteSpace(StateObject.MediaHandler.Artist))
            {
                StateObject.MediaHandler.SetMediaTagValue("Artist", artistMatches.First().Groups[1].Value);
            }
        }

        StateObject.MediaHandler.SetMediaTagValue("Title", StateObject.MediaHandler.Title.Trim());
        StateObject.MediaHandler.SetMediaTagValue("Artist", StateObject.MediaHandler.Artist.Trim());

        if (!string.Equals(oldArtist, StateObject.MediaHandler.Artist) ||
            !string.Equals(oldTitle, StateObject.MediaHandler.Title))
        {
            SimpleRuleEngine ruleEngine = new SimpleRuleEngine();
            ruleEngine.AddRule<TagFileAcoustIdRule>();
            ruleEngine.AddRule<TagFileMetadataApiRule>();
            ruleEngine.AddRule<TagFileTidalRule>();
            var results = await ruleEngine.RunAsync(StateObject);
            result.Success = results.Any(r => r.Success);
        }
        
        return result;
    }
}