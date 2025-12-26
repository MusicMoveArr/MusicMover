using MusicMover.Rules.Machine;

namespace MusicMover.Rules;

public class FileExistsRule : Rule
{
    public override bool Required { get; } = true;
    public override ContinueType ContinueType { get; } = ContinueType.Stop;
    
    public override async Task<StateResult> ExecuteAsync()
    {
        return new StateResult(StateObject.MediaHandler.FileInfo.Exists,
            StateObject.MediaHandler.FileInfo.Exists ? string.Empty : "Media file no longer exists");
    }
}