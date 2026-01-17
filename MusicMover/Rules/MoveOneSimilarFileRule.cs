using System.Runtime.Caching;
using MusicMover.Helpers;
using MusicMover.MediaHandlers;
using MusicMover.Rules.Machine;
using MusicMover.Services;

namespace MusicMover.Rules;

public class MoveOneSimilarFileRule : Rule
{
    public override bool Required => StateObject.SimilarFileResult.SimilarFiles.Count == 1 && !StateObject.Options.OnlyNewFiles;
    public override ContinueType ContinueType { get; } =  ContinueType.Continue;
    private readonly MediaTagWriteService _mediaTagWriteService;
    private readonly MemoryCache _memoryCache;

    public MoveOneSimilarFileRule()
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
        
        var similarFile = StateObject.SimilarFileResult.SimilarFiles.First();

        bool isFromPreferredQuality = StateObject.Options.PreferredFileExtensions.Any(ext => StateObject.MediaHandler.FileInfo.Extension.Contains(ext));
        bool isFromNonPreferredQuality = StateObject.Options.NonPreferredFileExtensions.Any(ext => StateObject.MediaHandler.FileInfo.Extension.Contains(ext));
        bool isSimilarPreferredQuality = StateObject.Options.PreferredFileExtensions.Any(ext => similarFile.File.Extension.Contains(ext));
        bool isNonPreferredQuality = StateObject.Options.NonPreferredFileExtensions.Any(ext => similarFile.File.Extension.Contains(ext));
        bool inputIsOutput = string.Equals(similarFile.File.FullName, StateObject.MediaHandler.FileInfo.FullName);

        if (inputIsOutput)
        {
            if (!StateObject.ToAlbumDirInfo.Exists)
            {
                StateObject.ToAlbumDirInfo.Create();
                MoveProcessor.IncrementCounter(() => MoveProcessor.CreatedSubDirectories++);
                Logger.WriteLine($"Created directory, {StateObject.ToAlbumDirInfo.FullName}", true);
            }

            await MediaFileHelper.UpdateArtistTagAsync(StateObject.MediaHandler, StateObject.MediaHandler.CleanArtist, StateObject.Options);

            await StateObject.MediaHandler.GenerateSaveFingerprintAsync();

            if (await _mediaTagWriteService.SafeSaveAsync(StateObject.MediaHandler, new FileInfo(newFromFilePath)))
            {
                MediaFileHelper.DumpCoverArt(StateObject.MediaHandler, StateObject.ToAlbumDirInfo, StateObject.Options);
                if (!string.Equals(StateObject.MediaHandler.FileInfo.FullName, newFromFilePath))
                {
                    StateObject.MediaHandler.FileInfo.Delete();
                }
                
                Logger.WriteLine($"Updated {StateObject.MediaHandler.FileInfo.Name} >> {newFromFilePath}", true);
                RemoveCacheByPath(similarFile.File.FullName);
                Logger.WriteLine($"Similar file found, overwritten input file, {StateObject.SimilarFileResult.SimilarFiles.Count}, {StateObject.MediaHandler.CleanArtist}/{StateObject.MediaHandler.Album}, {StateObject.MediaHandler.FileInfo.FullName}", true);
            }
            else
            {
                Logger.WriteLine("Failed to update/write to the file...??");
            }
        }
        else if (!isFromPreferredQuality && isSimilarPreferredQuality)
        {
            if (StateObject.Options.DeleteDuplicateFrom)
            {
                StateObject.MediaHandler.FileInfo.Delete();
                MoveProcessor.IncrementCounter(() => MoveProcessor.LocalDelete++);
                Logger.WriteLine($"Similar file found, deleted from file, quality is lower, {StateObject.SimilarFileResult.SimilarFiles.Count}, {StateObject.MediaHandler.CleanArtist}/{StateObject.MediaHandler.Album}, {StateObject.MediaHandler.FileInfo.FullName}");
            }
            else
            {
                Logger.WriteLine($"Similar file found, quality is lower, {StateObject.SimilarFileResult.SimilarFiles.Count}, {StateObject.MediaHandler.CleanArtist}/{StateObject.MediaHandler.Album}, {StateObject.MediaHandler.FileInfo.FullName}");
            }
        }
        else if (isFromPreferredQuality && isNonPreferredQuality || //overwrite lower quality based on extension
                 (isFromPreferredQuality && isSimilarPreferredQuality && StateObject.MediaHandler.FileInfo.Length > similarFile.File.Length) || //overwrite based on filesize, both high quality
                 (isFromNonPreferredQuality && isNonPreferredQuality && StateObject.MediaHandler.FileInfo.Length > similarFile.File.Length)) //overwrite based on filesize, both low quality
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
                Logger.WriteLine($"Moved {StateObject.MediaHandler.FileInfo.Name} >> {newFromFilePath}", true);
                
                if (similarFile.File.FullName != newFromFilePath && StateObject.Options.DeleteDuplicateTo)
                {
                    similarFile.File.Delete();
                    MoveProcessor.IncrementCounter(() => MoveProcessor.RemoteDelete++);
                    Logger.WriteLine($"Deleting duplicated file '{similarFile.File.FullName}'");
                }
                MoveProcessor.IncrementCounter(() => MoveProcessor.MovedFiles++);
                Logger.WriteLine($"Similar file found, overwriting target, From is bigger, {StateObject.SimilarFileResult.SimilarFiles.Count}, {StateObject.MediaHandler.CleanArtist}/{StateObject.MediaHandler.Album}, {StateObject.MediaHandler.FileInfo.FullName}", true);
            }
            
            RemoveCacheByPath(newFromFilePath);
            RemoveCacheByPath(similarFile.File.FullName);
        }
        else if (similarFile.File.Length == StateObject.MediaHandler.FileInfo.Length)
        {
            if (StateObject.Options.DeleteDuplicateFrom)
            {
                StateObject.MediaHandler.FileInfo.Delete();
                MoveProcessor.IncrementCounter(() => MoveProcessor.LocalDelete++);
                Logger.WriteLine($"Similar file found, deleted from file, exact same size from/target, {StateObject.SimilarFileResult.SimilarFiles.Count}, {StateObject.MediaHandler.CleanArtist}/{StateObject.MediaHandler.Album}, {StateObject.MediaHandler.FileInfo.FullName}", true);
            }
        }
        else if (similarFile.File.Length > StateObject.MediaHandler.FileInfo.Length)
        {
            if (StateObject.Options.DeleteDuplicateFrom)
            {
                StateObject.MediaHandler.FileInfo.Delete();
                MoveProcessor.IncrementCounter(() => MoveProcessor.LocalDelete++);
                Logger.WriteLine($"Similar file found, deleted from file, Target is bigger, {StateObject.SimilarFileResult.SimilarFiles.Count}, {StateObject.MediaHandler.CleanArtist}/{StateObject.MediaHandler.Album}, {StateObject.MediaHandler.FileInfo.FullName}", true);
            }
        }
        else
        {
            Logger.WriteLine($"[To Be Implemented] Similar file found {StateObject.SimilarFileResult.SimilarFiles.Count}, {StateObject.MediaHandler.CleanArtist}/{StateObject.MediaHandler.Album}, {similarFile.File.Extension}");
            return new StateResult(false, $"[To Be Implemented] Similar file found {StateObject.SimilarFileResult.SimilarFiles.Count}");
        }

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