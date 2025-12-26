using MusicMover.Helpers;
using MusicMover.Rules.Machine;

namespace MusicMover.Rules;

public class CreateArtistDirectoryRule : Rule
{
    public override bool Required => !Directory.Exists(StateObject.ToArtistDirInfo.FullName) && 
                                     StateObject.Options.CreateArtistDirectory &&
                                     !StateObject.Options.IsDryRun;
    public override ContinueType ContinueType { get; } =  ContinueType.Stop;
    public override async Task<StateResult> ExecuteAsync()
    {
        string artistFormat = ArtistHelper.GetFormatName(StateObject.MediaHandler, StateObject.Options.ArtistDirectoryFormat, StateObject.Options.DirectorySeperator);
        bool artistExists = StateObject.Options.ArtistDirsMustNotExist.Any(dir =>
        {
            var extraToArtistDirInfo = new DirectoryInfo(Path.Join(dir, artistFormat));
            return extraToArtistDirInfo.Exists;
        });
        
        if (!artistExists)
        {
            StateObject.ToArtistDirInfo.Create();
        }

        return new StateResult(StateObject.ToArtistDirInfo.Exists, 
            StateObject.ToArtistDirInfo.Exists ? string.Empty : $"Artist {StateObject.MediaHandler.CleanArtist} does not exist");
    }
}