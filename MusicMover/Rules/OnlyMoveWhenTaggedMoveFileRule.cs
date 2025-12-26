using MusicMover.Helpers;
using MusicMover.Rules.Machine;

namespace MusicMover.Rules;

public class OnlyMoveWhenTaggedMoveFileRule : Rule
{
    public override bool Required => StateObject.Options.OnlyMoveWhenTagged &&
                                     !StateObject.MusicBrainzTaggingSuccess &&
                                     !StateObject.TidalTaggingSuccess &&
                                     !StateObject.MetadataApiTaggingSuccess;
    
    
    public override ContinueType ContinueType { get; } = ContinueType.Stop;
    public override async Task<StateResult> ExecuteAsync()
    {
        //foreach (var plugin in _plugins)
        //{
        //    //plugin.TaggingFailed(mediaHandler);
        //}
        
        string artistFolderName = ArtistHelper.GetFormatName(StateObject.MediaHandler, StateObject.Options.ArtistDirectoryFormat, StateObject.Options.DirectorySeperator);
        string albumFolderName = ArtistHelper.GetFormatName(StateObject.MediaHandler, StateObject.Options.AlbumDirectoryFormat, StateObject.Options.DirectorySeperator);
        StateResult result = new StateResult(false, "Skipped processing, tagging failed");

        if (!string.IsNullOrWhiteSpace(StateObject.Options.MoveUntaggableFilesPath))
        {
            if (string.IsNullOrWhiteSpace(artistFolderName))
            {
                artistFolderName = "[unknown_artist]";
            }
            if (string.IsNullOrWhiteSpace(albumFolderName))
            {
                albumFolderName = "[unknown_album]";
            }
                
            artistFolderName = string.Join("_", artistFolderName.Split(Path.GetInvalidFileNameChars()));
            albumFolderName = string.Join("_", albumFolderName.Split(Path.GetInvalidFileNameChars()));
            string safeFileName = string.Join("_", StateObject.MediaHandler.FileInfo.Name.Split(Path.GetInvalidFileNameChars()));
                
            string newFilePath = Path.Join(
                StateObject.Options.MoveUntaggableFilesPath, 
                artistFolderName, 
                albumFolderName, 
                safeFileName);
            
            result.LogInfo($"Moving untaggable file to '{newFilePath}'");
                
            FileInfo newFilePathInfo = new FileInfo(newFilePath);
            if (!newFilePathInfo.Directory.Exists)
            {
                newFilePathInfo.Directory.Create();
            }
            File.Move(StateObject.MediaHandler.FileInfo.FullName, newFilePath, true);
        }
            
        return result;
    }
}