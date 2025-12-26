using MusicMover.Rules.Machine;

namespace MusicMover.Rules;

public class CheckRequiredTagsRule : Rule
{
    public override bool Required => string.IsNullOrWhiteSpace(StateObject.MediaHandler.CleanArtist) ||
                                     string.IsNullOrWhiteSpace(StateObject.MediaHandler.Album) ||
                                     string.IsNullOrWhiteSpace(StateObject.MediaHandler.Title);
    public override ContinueType ContinueType { get; } =  ContinueType.Stop;
    public override async Task<StateResult> ExecuteAsync()
    {
        return new StateResult(false, "File is missing Artist, Album or title in the tags, skipping");
    }
}