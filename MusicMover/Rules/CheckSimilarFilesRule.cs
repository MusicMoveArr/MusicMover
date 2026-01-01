using System.Runtime.Caching;
using FuzzySharp;
using MusicMover.Helpers;
using MusicMover.MediaHandlers;
using MusicMover.Models;
using MusicMover.Rules.Machine;
using MusicMover.Services;

namespace MusicMover.Rules;

public class CheckSimilarFilesRule : Rule
{
    private const int CacheTime = 5; //minutes
    private const int NamingAccuracy = 98;
    public override bool Required { get; } = true;
    public override ContinueType ContinueType { get; } = ContinueType.Stop;
    private readonly MemoryCache _memoryCache;
    private readonly FingerPrintService _fingerPrintService;

    public CheckSimilarFilesRule()
    {
        _memoryCache = MemoryCache.Default;
        _fingerPrintService = new FingerPrintService();
    }
    
    public override async Task<StateResult> ExecuteAsync()
    {
        StateObject.SimilarFileResult = await GetSimilarFilesAsync(
            StateObject.MediaHandler, 
            StateObject.MediaHandler.TargetSaveFileInfo, 
            StateObject.ToAlbumDirInfo, 
            StateObject.Options);

        if (StateObject.SimilarFileResult.Errors && !StateObject.Options.ContinueScanError)
        {
            return new StateResult(false, "Scan errors... skipping");
        }

        string artistFolderName = ArtistHelper.GetFormatName(StateObject.MediaHandler, StateObject.Options.ArtistDirectoryFormat, StateObject.Options.DirectorySeperator);
        string albumFolderName = ArtistHelper.GetFormatName(StateObject.MediaHandler, StateObject.Options.AlbumDirectoryFormat, StateObject.Options.DirectorySeperator);
        
        bool extraDirExists = true;
        foreach (string extraScaDir in StateObject.Options.ExtraScans)
        {
            DirectoryInfo extraScaDirInfo = new DirectoryInfo(extraScaDir);
            string albumPath = DirectoryHelper.GetDirectoryCaseInsensitive(extraScaDirInfo, Path.Join(artistFolderName, albumFolderName));
            DirectoryInfo albumDirInfo = new DirectoryInfo(Path.Join(extraScaDir, albumPath));

            if (!albumDirInfo.Exists)
            {
                extraDirExists = false;
                continue;
            }

            var extraSimilarResult = await GetSimilarFilesAsync(StateObject.MediaHandler, StateObject.MediaHandler.TargetSaveFileInfo, albumDirInfo, StateObject.Options);

            if (extraSimilarResult.Errors && !StateObject.Options.ContinueScanError)
            {
                break;
            }

            if (extraSimilarResult.SimilarFiles.Count > 0)
            {
                StateObject.SimilarFileResult.SimilarFiles.AddRange(extraSimilarResult.SimilarFiles);
            }
        }

        if (!extraDirExists && StateObject.Options.ExtraDirMustExist)
        {
            return new StateResult(false, $"Skipping file, artist '{StateObject.MediaHandler.CleanArtist}' does not exist in extra directory");
        }

        if (StateObject.SimilarFileResult.Errors && !StateObject.Options.ContinueScanError)
        {
            MoveProcessor.IncrementCounter(() => MoveProcessor.SkippedErrorFiles++);
            return new StateResult(false, "Scan errors... skipping");
        }

        return new StateResult(true);
    }
    
    private async Task<SimilarFileResult> GetSimilarFilesAsync(
        MediaHandler mediaHandler, 
        FileInfo fromFileInfo,
        DirectoryInfo toAlbumDirInfo,
        CliOptions options)
    {
        SimilarFileResult similarFileResult = new SimilarFileResult();

        if (!toAlbumDirInfo.Exists)
        {
            return similarFileResult;
        }
        
        List<FileInfo> toFiles = toAlbumDirInfo
            .GetFiles("*.*", SearchOption.AllDirectories)
            .Where(file => file.Length > 0)
            .Where(file => MoveProcessor.MediaFileExtensions.Any(ext => file.Name.ToLower().EndsWith(ext)))
            .ToList();

        if (options.OnlyFileNameMatching)
        {
            var similarFiles = toFiles
                .Where(toFile => !string.Equals(toFile.FullName, fromFileInfo.FullName))
                .Where(toFile => FuzzyHelper.FuzzRatioToLower(toFile.Name.Replace(toFile.Extension, string.Empty),
                                            fromFileInfo.Name.Replace(fromFileInfo.Extension, string.Empty)) >= 95)
                .Select(toFile => new SimilarFileInfo(toFile))
                .ToList();

            similarFileResult.SimilarFiles.AddRange(similarFiles);
            return similarFileResult;
        }
        
        await mediaHandler.GenerateSaveFingerprintAsync();

        foreach (FileInfo toFile in toFiles.Where(toFile => !string.Equals(toFile.FullName, fromFileInfo.FullName)))
        {
            try
            {
                MediaHandler? cachedMediaHandler = null;

                lock (_memoryCache)
                {
                    cachedMediaHandler = _memoryCache.Get(toFile.FullName) as MediaHandler;
                }

                if (cachedMediaHandler == null)
                {
                    cachedMediaHandler = await AddFileToCacheAsync(toFile, options);
                }
                else
                {
                    MoveProcessor.IncrementCounter(() => MoveProcessor.CachedReadTargetFiles++);
                }
                
                if (cachedMediaHandler == null)
                {
                    similarFileResult.Errors = true;
                    continue;
                }

                await cachedMediaHandler.GenerateSaveFingerprintAsync();

                double similarity = _fingerPrintService.DTWSimilarity(
                    mediaHandler.AcoustIdFingerprintData,
                    cachedMediaHandler.AcoustIdFingerprintData);

                if (similarity >= 0.99D)
                {
                    similarFileResult.SimilarFiles.Add(new SimilarFileInfo(toFile, cachedMediaHandler));
                }
            }
            catch (Exception e)
            {
                similarFileResult.Errors = true;
                Logger.WriteLine($"scan error file, {e.Message}, {toFile.FullName}");
            }
        }

        return similarFileResult;
    }

    private async Task<MediaHandler?> AddFileToCacheAsync(FileInfo fileInfo, CliOptions options)
    {
        MoveProcessor.IncrementCounter(() => MoveProcessor.ScannedTargetFiles++);
        MediaHandler? cachedMediaInfo = await MediaFileHelper.GetMediaFileHandlerAsync(fileInfo, options);

        if (cachedMediaInfo is null)
        {
            return null;
        }

        lock (_memoryCache)
        {
            _memoryCache.Add(fileInfo.FullName, cachedMediaInfo, DateTimeOffset.Now.AddMinutes(CacheTime));
        }

        return cachedMediaInfo;
    }
}