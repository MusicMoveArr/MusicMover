using MusicMover.Helpers;
using MusicMover.Rules.Machine;

namespace MusicMover.Rules;

public class OnlyNewFilesDeleteRule : Rule
{
    public override bool Required => StateObject.SimilarFileResult.SimilarFiles.Count >= 1 && 
                                     StateObject.Options.OnlyNewFiles && 
                                     StateObject.Options.DeleteDuplicateFrom;
    public override ContinueType ContinueType { get; } =  ContinueType.Continue;

    public OnlyNewFilesDeleteRule()
    {
        
    }
    
    public override async Task<StateResult> ExecuteAsync()
    {
        StateObject.MediaHandler.FileInfo.Delete();
        MoveProcessor.IncrementCounter(() => MoveProcessor.LocalDelete++);
        Logger.WriteLine($"Similar file found, deleted from file, {StateObject.SimilarFileResult.SimilarFiles.Count}, {StateObject.MediaHandler.CleanArtist}/{StateObject.MediaHandler.Album}, {StateObject.MediaHandler.FileInfo.FullName}", true);

        return new StateResult(true);
    }
}