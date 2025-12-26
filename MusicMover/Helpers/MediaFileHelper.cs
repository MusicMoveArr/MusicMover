using MusicMover.MediaHandlers;
using MusicMover.Services;

namespace MusicMover.Helpers;

public class MediaFileHelper
{
    public static async Task UpdateArtistTagAsync(MediaHandler mediaHandler, string artist, CliOptions options)
    {
        if (options.UpdateArtistTags)
        {
            MediaTagWriteService mediaTagWriteService = new MediaTagWriteService();
            await mediaTagWriteService.UpdateArtistAsync(mediaHandler, artist);
            MoveProcessor.IncrementCounter(() => MoveProcessor.UpdatedTagfiles++);
        }
    }
    
    public static void DumpCoverArt(MediaHandler mediaHandler, DirectoryInfo toAlbumDirInfo, CliOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.DumpCoverFilename))
        {
            mediaHandler.DumpCover(new FileInfo(Path.Join(toAlbumDirInfo.FullName, options.DumpCoverFilename)));
        }
    }
    
    public static async Task<MediaHandler?> GetMediaFileHandlerAsync(FileInfo fromFile, CliOptions options)
    {
        MediaHandler mediaHandler = null;
            
        try
        {
            switch (options.MetadataHandlerLibrary)
            {
                case MoveProcessor.MediaHandlerATLCore:
                    mediaHandler = new MediaHandlerAtlCore(fromFile);
                    break;
                case MoveProcessor.MediaHandlerFFmpeg:
                    mediaHandler = new MediaHandlerFFmpeg(fromFile);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"{ex.Message}, {fromFile.FullName}");
            MoveProcessor.IncrementCounter(() => MoveProcessor.SkippedErrorFiles++);
        }

        try
        {
            CorruptionFixer corruptionFixer = new CorruptionFixer();
            if (mediaHandler is null &&
                options.FixFileCorruption &&
                await corruptionFixer.FixCorruptionAsync(fromFile))
            {
                switch (options.MetadataHandlerLibrary)
                {
                    case MoveProcessor.MediaHandlerATLCore:
                        mediaHandler = new MediaHandlerAtlCore(fromFile);
                        break;
                    case MoveProcessor.MediaHandlerFFmpeg:
                        mediaHandler = new MediaHandlerFFmpeg(fromFile);
                        break;
                }
                MoveProcessor.IncrementCounter(() => MoveProcessor.FixedCorruptedFiles++);
            }
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"{ex.Message}, {fromFile.FullName}");
            MoveProcessor.IncrementCounter(() => MoveProcessor.SkippedErrorFiles++);
        }
        return mediaHandler;
    }
}