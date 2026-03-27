using System.Runtime.Caching;
using FuzzySharp;
using MusicMover.Helpers;
using MusicMover.MediaHandlers;
using MusicMover.Models;
using MusicMover.Rules.Machine;
using MusicMover.Services;

namespace MusicMover.Rules;

public class CheckEarlySimilarFilesRule : Rule
{
    public override bool Required { get; } = true;
    public override ContinueType ContinueType { get; } = ContinueType.Stop;

    public CheckEarlySimilarFilesRule()
    {
        
    }
    
    public override async Task<StateResult> ExecuteAsync()
    {
        if (StateObject.ToArtistDirInfo.FullName == new DirectoryInfo(StateObject.Options.ToDirectory).FullName)
        {
            return new StateResult(true, "Can't check, ArtistDirectory is the same as Target Music Directory");
        }
        
        if (string.IsNullOrWhiteSpace(StateObject.MediaHandler.Artist) ||
            string.IsNullOrWhiteSpace(StateObject.MediaHandler.Album) ||
            string.IsNullOrWhiteSpace(StateObject.MediaHandler.Title))
        {
            return new StateResult(true, "Can't check, no Artist or Album or Title");
        }
        
        SimpleRuleEngine ruleEngine = new SimpleRuleEngine();
        ruleEngine.AddRule<CheckSimilarFilesRule>();
        ruleEngine.AddRule<OnlyNewFilesDeleteRule>();
        await ruleEngine.RunAsync(StateObject);

        return new StateResult(StateObject.MediaHandler.FileInfo.Exists);
    }
}