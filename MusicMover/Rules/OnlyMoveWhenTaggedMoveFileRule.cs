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

        artistFolderName = artistFolderName.Replace("\0", string.Empty);
        albumFolderName = albumFolderName.Replace("\0", string.Empty);
        
        if (!string.IsNullOrWhiteSpace(StateObject.Options.MoveUntaggableFilesPath))
        {
            if (string.IsNullOrWhiteSpace(artistFolderName.Replace("/", string.Empty).Replace("\\", string.Empty)))
            {
                artistFolderName = "[unknown_artist]";
            }
            if (string.IsNullOrWhiteSpace(albumFolderName.Replace("/", string.Empty).Replace("\\", string.Empty)))
            {
                albumFolderName = "[unknown_album]";
            }
            
            string newFilePath = Path.Join(
                StateObject.Options.MoveUntaggableFilesPath, 
                artistFolderName, 
                albumFolderName, 
                StateObject.MediaHandler.FileInfo.Name);
            
            result.LogInfo($"Moving untaggable file to '{newFilePath}'");
                
            FileInfo newFilePathInfo = new FileInfo(newFilePath);

            var sure = newFilePathInfo.Directory.FullName
                .Substring(StateObject.Options.MoveUntaggableFilesPath.Length)
                .Split('/')
                .ToList();
            
            if (!newFilePathInfo.Directory.Exists)
            {
                newFilePathInfo.Directory.Create();
            }
            File.Move(StateObject.MediaHandler.FileInfo.FullName, newFilePath, true);
        }
            
        return result;
    }
}