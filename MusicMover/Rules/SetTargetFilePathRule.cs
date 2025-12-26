using MusicMover.Helpers;
using MusicMover.Rules.Machine;

namespace MusicMover.Rules;

public class SetTargetFilePathRule : Rule
{
    public override bool Required => !string.IsNullOrWhiteSpace(StateObject.Options.FileFormat);
    public override ContinueType ContinueType { get; } =  ContinueType.Continue;
    public override async Task<StateResult> ExecuteAsync()
    {
        string newFileName = DirectoryHelper.SanitizeFileName(ArtistHelper.GetFormatName(StateObject.MediaHandler, StateObject.Options.FileFormat, StateObject.Options.DirectorySeperator)) + StateObject.MediaHandler.FileInfo.Extension;
        string newFilePath = Path.Join(StateObject.MediaHandler.FileInfo.Directory.FullName, newFileName);
        StateObject.MediaHandler.TargetSaveFileInfo = new FileInfo(newFilePath);
        return new StateResult(true);
    }
}