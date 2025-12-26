using System.Runtime.Caching;
using MusicMover.Helpers;
using MusicMover.MediaHandlers;
using MusicMover.Rules.Machine;
using MusicMover.Services;

namespace MusicMover.Rules;

public class MoveMultipleSimilarFilesRule : Rule
{
    public override bool Required => StateObject.SimilarFileResult.SimilarFiles.Count > 1;
    public override ContinueType ContinueType { get; } =  ContinueType.Continue;
    private readonly MediaTagWriteService _mediaTagWriteService;
    private readonly MemoryCache _memoryCache;

    public MoveMultipleSimilarFilesRule()
    {
        _mediaTagWriteService = new MediaTagWriteService();
        _memoryCache = MemoryCache.Default;
    }
    
    public override async Task<StateResult> ExecuteAsync()
    {
        string fromFileName = StateObject.MediaHandler.TargetSaveFileInfo.Name;

        if (StateObject.Options.RenameVariousArtists &&
            fromFileName.Contains(MediaHandler.VariousArtistsName))
        {
            fromFileName = fromFileName.Replace(MediaHandler.VariousArtistsName, StateObject.MediaHandler.CleanArtist);
        }
        string newFromFilePath = Path.Join(StateObject.ToAlbumDirInfo.FullName, fromFileName);
        
        bool isFromPreferredQuality = StateObject.Options.PreferredFileExtensions.Any(ext => StateObject.MediaHandler.FileInfo.Extension.Contains(ext));
        bool isNonPreferredQuality = StateObject.Options.NonPreferredFileExtensions.Any(ext => StateObject.SimilarFileResult.SimilarFiles.Any(similarFile => similarFile.File.Extension.Contains(ext)));
        
        bool inputIsOutput = StateObject.SimilarFileResult.SimilarFiles
            .Any(sim => string.Equals(sim.File.FullName, StateObject.MediaHandler.FileInfo.FullName));
        
        if (isFromPreferredQuality && isNonPreferredQuality)
        {
            if (!StateObject.ToAlbumDirInfo.Exists)
            {
                StateObject.ToAlbumDirInfo.Create();
                MoveProcessor.IncrementCounter(() => MoveProcessor.CreatedSubDirectories++);
                Logger.WriteLine($"Created directory, {StateObject.ToAlbumDirInfo.FullName}", true);
            }

            await MediaFileHelper.UpdateArtistTagAsync(StateObject.MediaHandler, StateObject.MediaHandler.CleanArtist, StateObject.Options);

            await StateObject.MediaHandler.GenerateSaveFingerprintAsync();

            bool saveSuccess = false;
            if ((saveSuccess = await _mediaTagWriteService.SafeSaveAsync(StateObject.MediaHandler, new FileInfo(newFromFilePath))) &&
                !string.Equals(StateObject.MediaHandler.FileInfo.FullName, newFromFilePath))
            {
                MediaFileHelper.DumpCoverArt(StateObject.MediaHandler, StateObject.ToAlbumDirInfo, StateObject.Options);
                StateObject.MediaHandler.FileInfo.Delete();
            }

            if (saveSuccess)
            {
                MediaFileHelper.DumpCoverArt(StateObject.MediaHandler, StateObject.ToAlbumDirInfo, StateObject.Options);
                
                Logger.WriteLine($"Moved {StateObject.MediaHandler.FileInfo.Name} >> {newFromFilePath}");
                RemoveCacheByPath(newFromFilePath);

                if (StateObject.Options.DeleteDuplicateTo)
                {
                    StateObject.SimilarFileResult.SimilarFiles.ForEach(similarFile =>
                    {
                        inputIsOutput = string.Equals(similarFile.File.FullName, StateObject.MediaHandler.FileInfo.FullName) ||
                                        string.Equals(similarFile.File.FullName, newFromFilePath);
                        if (similarFile.File.FullName != newFromFilePath && !inputIsOutput)
                        {
                            Logger.WriteLine($"Deleting duplicated file '{similarFile.File.FullName}'");
                            RemoveCacheByPath(similarFile.File.FullName);
                            similarFile.File.Delete();
                            MoveProcessor.IncrementCounter(() => MoveProcessor.RemoteDelete++);
                        }
                    });
                }

                MoveProcessor.IncrementCounter(() => MoveProcessor.MovedFiles++);
                Logger.WriteLine($"Similar files found, overwriting target, From is bigger, {StateObject.SimilarFileResult.SimilarFiles.Count}, {StateObject.MediaHandler.CleanArtist}/{StateObject.MediaHandler.Album}, {StateObject.MediaHandler.FileInfo.FullName}");
            }
        }
        else if(StateObject.Options.DeleteDuplicateFrom && !inputIsOutput)
        {
            StateObject.MediaHandler.FileInfo.Delete();
            MoveProcessor.IncrementCounter(() => MoveProcessor.LocalDelete++);
            Logger.WriteLine($"Similar files found, deleted from file, Targets are bigger, {StateObject.SimilarFileResult.SimilarFiles.Count}, {StateObject.MediaHandler.CleanArtist}/{StateObject.MediaHandler.Album}, {StateObject.MediaHandler.FileInfo.FullName}");
        }

        Logger.WriteLine($"Similar files found {StateObject.SimilarFileResult.SimilarFiles.Count}, {StateObject.MediaHandler.CleanArtist}/{StateObject.MediaHandler.Album}");
        return new StateResult(true);
    }
    
    private void RemoveCacheByPath(string fullPath)
    {
        lock (_memoryCache)
        {
            var cachedMediaInfo = _memoryCache.Get(fullPath) as MediaHandler;
            if (cachedMediaInfo != null)
            {
                _memoryCache.Remove(fullPath, CacheEntryRemovedReason.Removed);
            }
        }
    }
}