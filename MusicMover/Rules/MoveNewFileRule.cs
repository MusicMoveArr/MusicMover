using System.Runtime.Caching;
using MusicMover.Helpers;
using MusicMover.MediaHandlers;
using MusicMover.Rules.Machine;
using MusicMover.Services;

namespace MusicMover.Rules;

public class MoveNewFileRule : Rule
{
    private readonly MediaTagWriteService _mediaTagWriteService;
    public override bool Required => StateObject.SimilarFileResult.SimilarFiles.Count == 0;
    
    public override ContinueType ContinueType { get; } =  ContinueType.Continue;
    private readonly MemoryCache _memoryCache;

    public MoveNewFileRule()
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
        
        Logger.WriteLine($"No similar files found moving, {StateObject.MediaHandler.CleanArtist}/{StateObject.MediaHandler.Album}, {newFromFilePath}", true);
            
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
            Logger.WriteLine($"Moved {StateObject.MediaHandler.FileInfo.Name} >> {newFromFilePath}", true);
            MoveProcessor.IncrementCounter(() => MoveProcessor.MovedFiles++);
        }
        RemoveCacheByPath(newFromFilePath);
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