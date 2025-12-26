using MusicMover.Helpers;
using MusicMover.Rules.Machine;

namespace MusicMover.Rules;

public class SetToArtistDirectoryRule : Rule
{
    public override bool Required { get; } = true;
    public override ContinueType ContinueType { get; } = ContinueType.Stop;
    public override async Task<StateResult> ExecuteAsync()
    {
        DirectoryInfo musicDirInfo = new DirectoryInfo(StateObject.Options.ToDirectory);

        string artistFormat = ArtistHelper.GetFormatName(StateObject.MediaHandler, StateObject.Options.ArtistDirectoryFormat, StateObject.Options.DirectorySeperator);
        string albumFormat = ArtistHelper.GetFormatName(StateObject.MediaHandler, StateObject.Options.AlbumDirectoryFormat, StateObject.Options.DirectorySeperator);

        artistFormat = artistFormat.Replace(char.MinValue.ToString(), string.Empty);
        albumFormat = albumFormat.Replace(char.MinValue.ToString(), string.Empty);
        
        string artistPath = DirectoryHelper.GetDirectoryCaseInsensitive(musicDirInfo, artistFormat);
        string albumPath = DirectoryHelper.GetDirectoryCaseInsensitive(musicDirInfo, Path.Join(artistFormat, albumFormat));

        StateObject.ToArtistDirInfo = new DirectoryInfo(Path.Join(StateObject.Options.ToDirectory, artistPath));
        StateObject.ToAlbumDirInfo = new DirectoryInfo(Path.Join(StateObject.Options.ToDirectory, albumPath));

        return new StateResult(true);
    }
}