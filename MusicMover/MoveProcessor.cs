using System.Diagnostics;
using System.Runtime.Caching;
using FuzzySharp;
using ListRandomizer;
using MusicMover.Helpers;
using MusicMover.Models;
using MusicMover.Services;
using SmartFormat;
using Spectre.Console;

namespace MusicMover;

public class MoveProcessor
{
    public static readonly string[] MediaFileExtensions = new string[]
    {
        "flac",
        "mp3",
        "m4a",
        "wav",
        "aaif",
        "opus"
    };

    private const long MinAvailableDiskSpace = 5000; //GB
    private const int CacheTime = 5; //minutes
    private const string VariousArtistsName = "Various Artists";

    private const int NamingAccuracy = 98;
    
    private int _movedFiles = 0;
    private int _localDelete = 0;
    private int _remoteDelete = 0;
    private int _createdSubDirectories = 0;
    private int _scannedFromFiles = 0;
    private int _skippedErrorFiles = 0;
    private int _scannedTargetFiles = 0;
    private int _cachedReadTargetFiles = 0;
    private int _fixedCorruptedFiles = 0;
    private object _counterLock = new object();
    private int _updatedTagfiles = 0;
    private bool _exitProcess = false;

    private Stopwatch _sw = Stopwatch.StartNew();
    private Stopwatch _runtimeSw = Stopwatch.StartNew();

    private readonly CliOptions _options;
    private readonly MemoryCache _memoryCache;
    private readonly CorruptionFixer _corruptionFixer;
    private readonly MusicBrainzService _musicBrainzService;
    private readonly TidalService _tidalService;
    private readonly MiniMediaMetadataService _miniMediaMetadataService;
    private readonly AsyncLock _asyncAcoustIdLock;
    private readonly FingerPrintService _fingerPrintService;
    
    private List<string> _artistsNotFound = new List<string>();

    public MoveProcessor(CliOptions options)
    {
        _options = options;
        _asyncAcoustIdLock = new AsyncLock();
        _memoryCache = MemoryCache.Default;
        _corruptionFixer = new CorruptionFixer();
        _musicBrainzService = new MusicBrainzService();
        _fingerPrintService = new FingerPrintService();
        _tidalService = new TidalService(options.TidalClientId, options.TidalClientSecret, options.TidalCountryCode);
        _miniMediaMetadataService = new MiniMediaMetadataService(options.MetadataApiBaseUrl, options.MetadataApiProviders);
    }

    public async Task ProcessAsync()
    {
        List<MediaFileInfo> filesToProcess = new List<MediaFileInfo>();
        
        var topDirectories = Directory
            .EnumerateFileSystemEntries(_options.FromDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(file => Directory.Exists(file))
            .Where(dir => !dir.Contains(".Trash"))
            .Skip(_options.SkipFromDirAmount)
            .OrderBy(dir => dir)
            .ToList();
        
        filesToProcess.AddRange(await GetMediaFileListAsync(_options.FromDirectory, SearchOption.TopDirectoryOnly));

        int directoriesRead = 0;
        foreach (string directoryPath in topDirectories)
        {
            Logger.WriteLine($"[{directoriesRead++}/{topDirectories.Count}] Reading files from '{directoryPath}'");
            filesToProcess.AddRange(await GetMediaFileListAsync(directoryPath, SearchOption.AllDirectories));
        }

        filesToProcess = filesToProcess
            .OrderBy(file => file.Artist)
            .ThenBy(file => file.AlbumArtist)
            .ThenBy(file => file.Album)
            .ToList();

        //process directories
        await AnsiConsole.Progress()
            .HideCompleted(true)
            .AutoClear(true)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn()
                {
                    Alignment = Justify.Left
                },
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
            })
            .StartAsync(async ctx =>
            {
                Dictionary<string, ProgressTask> progressTasks = new Dictionary<string, ProgressTask>();
                var totalProgressTask = ctx.AddTask(Markup.Escape($"Processing tracks 0 of {filesToProcess.Count} processed"));
                totalProgressTask.MaxValue = filesToProcess.Count;
                if (_options.Parallel)
                {
                    AsyncLock progressLock = new AsyncLock();
                    
                    await ParallelHelper.ForEachAsync(filesToProcess, 5, async mediaFileInfo =>
                    {
                        using (await progressLock.LockAsync())
                        {
                            if (!progressTasks.ContainsKey(mediaFileInfo.Artist))
                            {
                                string artistName = mediaFileInfo.Artist;
                                if (artistName.Length > 50)
                                {
                                    artistName = artistName.Substring(0, 50) + "...";
                                }
                                int trackCount = filesToProcess.Count(file => string.Equals(file.Artist, mediaFileInfo.Artist));
                                var task = ctx.AddTask(Markup.Escape($"Processing Artist '{artistName}' 0 of {trackCount} processed, Album: '{mediaFileInfo.Album}', Track: '{mediaFileInfo.Title}'"));
                                task.MaxValue = trackCount;
                                progressTasks.TryAdd(mediaFileInfo.Artist,task);
                            }
                        }
                        
                        try
                        {
                            await ProcessFromFileAsync(mediaFileInfo);
                        }
                        catch (Exception e)
                        {
                            Logger.WriteLine($"{mediaFileInfo.FileInfo.FullName}, {e.Message}. \r\n{e.StackTrace}");
                            IncrementCounter(() => _skippedErrorFiles++);
                        }
                        
                        
                        using (await progressLock.LockAsync())
                        {
                            if (progressTasks.TryGetValue(mediaFileInfo.Artist, out ProgressTask progressTask))
                            {
                                progressTask.Increment(1);
                                progressTask.Description(Markup.Escape($"Processing Artist '{mediaFileInfo.Artist}' {progressTask.Value} of {progressTask.MaxValue} processed, Album: '{mediaFileInfo.Album}', Track: '{mediaFileInfo.Title}'"));
                            }
                        }
                        
                        totalProgressTask.Value++;
                        totalProgressTask.Description(Markup.Escape($"Processing tracks {totalProgressTask.Value} of {filesToProcess.Count} processed"));
                    });
                }
                else
                {
                    for(; filesToProcess.Count > 0; )
                    {
                        var mediaFileInfo = filesToProcess.FirstOrDefault();
                        filesToProcess.RemoveAt(0);
                        
                        if (!progressTasks.ContainsKey(mediaFileInfo.Artist))
                        {
                            string artistName = mediaFileInfo.Artist;
                            if (artistName.Length > 50)
                            {
                                artistName = artistName.Substring(0, 50) + "...";
                            }
                            int trackCount = filesToProcess.Count(file => string.Equals(file.Artist, mediaFileInfo.Artist));
                            var task = ctx.AddTask(Markup.Escape($"Processing Artist '{artistName}' 0 of {trackCount} processed, Album: '{mediaFileInfo.Album}', Track: '{mediaFileInfo.Title}'"));
                            task.MaxValue = trackCount;
                            progressTasks.TryAdd(mediaFileInfo.Artist,task);
                        }
                        try
                        {
                            await ProcessFromFileAsync(mediaFileInfo);
                        }
                        catch (Exception e)
                        {
                            Logger.WriteLine($"{mediaFileInfo.FileInfo.FullName}, {e.Message}. \r\n{e.StackTrace}");
                            IncrementCounter(() => _skippedErrorFiles++);
                        }
                        
                        if (progressTasks.TryGetValue(mediaFileInfo.Artist, out ProgressTask progressTask))
                        {
                            progressTask.Increment(1);
                            progressTask.Description(Markup.Escape($"Processing Artist '{mediaFileInfo.Artist}' {progressTask.Value} of {progressTask.MaxValue} processed, Album: '{mediaFileInfo.Album}', Track: '{mediaFileInfo.Title}'"));
                        }
                        
                        totalProgressTask.Value++;
                        totalProgressTask.Description(Markup.Escape($"Processing tracks {totalProgressTask.Value} of {filesToProcess.Count} processed"));
                    }
                }
            });

        ShowProgress();

        if (_artistsNotFound.Count > 0)
        {
            Logger.WriteLine($"Artists not found: {_artistsNotFound.Count}");
            _artistsNotFound.ForEach(artist => Logger.WriteLine(artist));
        }
    }

    private async Task<List<MediaFileInfo>> GetMediaFileListAsync(string fromTopDir, SearchOption dirSearchOption)
    {
        List<MediaFileInfo> filesToProcess = new List<MediaFileInfo>();
        DirectoryInfo fromDirInfo = new DirectoryInfo(fromTopDir);

        FileInfo[] fromFiles = fromDirInfo
            .GetFiles("*.*", dirSearchOption)
            .Where(file => file.Length > 0)
            .Where(file => MediaFileExtensions.Any(ext => file.Name.ToLower().EndsWith(ext)))
            .ToArray();

        foreach (FileInfo fromFile in fromFiles)
        {
            MediaFileInfo mediaFileInfo = null;
            
            try
            {
                mediaFileInfo = new MediaFileInfo(fromFile);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"{ex.Message}, {fromFile.FullName}");
                IncrementCounter(() => _skippedErrorFiles++);
            }

            try
            {
                if (mediaFileInfo is null &&
                    _options.FixFileCorruption &&
                    await _corruptionFixer.FixCorruptionAsync(fromFile))
                {
                    mediaFileInfo = new MediaFileInfo(fromFile);
                    IncrementCounter(() => _fixedCorruptedFiles++);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"{ex.Message}, {fromFile.FullName}");
                IncrementCounter(() => _skippedErrorFiles++);
            }

            if (mediaFileInfo != null)
            {
                filesToProcess.Add(mediaFileInfo);
            }
        }

        return filesToProcess;
    }

    private bool SetToArtistDirectory(string artist, 
        MediaFileInfo tagFile,
        out DirectoryInfo toArtistDirInfo,
        out DirectoryInfo toAlbumDirInfo)
    {
        DirectoryInfo musicDirInfo = new DirectoryInfo(_options.ToDirectory);
        string artistPath = GetDirectoryCaseInsensitive(musicDirInfo,SanitizeArtistName(artist));
        string albumPath = GetDirectoryCaseInsensitive(musicDirInfo, $"{SanitizeArtistName(artist)}/{SanitizeAlbumName(tagFile.Album)}");
        
        toArtistDirInfo = new DirectoryInfo($"{_options.ToDirectory}{artistPath}");
        toAlbumDirInfo = new DirectoryInfo($"{_options.ToDirectory}{albumPath}");

        if (!toArtistDirInfo.Exists &&
            _options.CreateArtistDirectory &&
            !_options.IsDryRun)
        {
            bool artistExists = _options.ArtistDirsMustNotExist.Any(dir =>
            {
                var extraToArtistDirInfo = new DirectoryInfo($"{dir}{SanitizeArtistName(artist)}");
                return extraToArtistDirInfo.Exists;
            });
            
            if (!artistExists)
            {
                toArtistDirInfo.Create();
            }
        }

        if (!toArtistDirInfo.Exists)
        {
            Logger.WriteLine($"Artist {artist} does not exist", true);
        }

        return toArtistDirInfo.Exists;
    }

    private async Task<bool> ProcessFromFileAsync(MediaFileInfo mediaFileInfo)
    {
        if (!EnoughDiskSpace())
        {
            if (!_exitProcess)
            {
                Logger.WriteLine("Not enough diskspace left! <5GB on target directory>");
            }
            _exitProcess = true;
            return false;
        }

        if (!mediaFileInfo.FileInfo.Exists)
        {
            Logger.WriteLine($"Media file no longer exists '{mediaFileInfo.FileInfo.FullName}'");
            return false;
        }
            
        lock (_sw)
        {
            if (_sw.Elapsed.Seconds >= 5)
            {
                ShowProgress();
                _sw.Restart();
            }
        }

        IncrementCounter(() => _scannedFromFiles++);
        Debug.WriteLine($"File: {mediaFileInfo.FileInfo.FullName}");

        if (await mediaFileInfo.GenerateSaveFingerprintAsync())
        {
            mediaFileInfo = new MediaFileInfo(mediaFileInfo.FileInfo);
        }

        bool metadataApiTaggingSuccess = false;
        bool musicBrainzTaggingSuccess = false;
        bool tidalTaggingSuccess = false;

        using (await _asyncAcoustIdLock.LockAsync())
        {
            if (!string.IsNullOrWhiteSpace(_options.AcoustIdApiKey) &&
                (_options.AlwaysCheckAcoustId ||
                 string.IsNullOrWhiteSpace(mediaFileInfo.Artist) ||
                 string.IsNullOrWhiteSpace(mediaFileInfo.Album) ||
                 string.IsNullOrWhiteSpace(mediaFileInfo.AlbumArtist)))
            {
                FpcalcOutput? fingerprint = await _fingerPrintService.GetFingerprintAsync(mediaFileInfo.FileInfo.FullName);
                var match = await _musicBrainzService.GetMatchFromAcoustIdAsync(mediaFileInfo,
                    fingerprint,
                    _options.AcoustIdApiKey,
                    _options.SearchByTagNames,
                    _options.AcoustIdMatchPercentage,
                    _options.MusicBrainzMatchPercentage);
                
                musicBrainzTaggingSuccess =
                    match != null && await _musicBrainzService.WriteTagFromAcoustIdAsync(
                        match,
                        mediaFileInfo, 
                        _options.OverwriteArtist, 
                        _options.OverwriteAlbum, 
                        _options.OverwriteTrack,
                        _options.OverwriteAlbumArtist);
            
                if (musicBrainzTaggingSuccess)
                {
                    //read again the file after saving
                    mediaFileInfo = new MediaFileInfo(mediaFileInfo.FileInfo);
                    Logger.WriteLine($"Updated with AcoustId/MusicBrainz tags {mediaFileInfo.FileInfo.FullName}", true);
                }
                else
                {
                    Logger.WriteLine($"AcoustId not found by Fingerprint for {mediaFileInfo.FileInfo.FullName}", true);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(_options.MetadataApiBaseUrl))
        {
            metadataApiTaggingSuccess = await _miniMediaMetadataService.WriteTagsAsync(mediaFileInfo, 
                mediaFileInfo.FileInfo, 
                ArtistHelper.GetUncoupledArtistName(mediaFileInfo.Artist), 
                ArtistHelper.GetUncoupledArtistName(mediaFileInfo.AlbumArtist),
                _options.OverwriteArtist, 
                _options.OverwriteAlbum, 
                _options.OverwriteTrack,
                _options.OverwriteAlbumArtist,
                _options.MetadataApiMatchPercentage);
            
            if (metadataApiTaggingSuccess)
            {
                //read again the file after saving
                mediaFileInfo = new MediaFileInfo(mediaFileInfo.FileInfo);
                Logger.WriteLine($"Updated with Tidal tags {mediaFileInfo.FileInfo.FullName}", true);
            }
            else
            {
                Logger.WriteLine($"Tidal track not found for {mediaFileInfo.FileInfo.FullName}", true);
            }
        }
        
        if (!metadataApiTaggingSuccess &&
            !string.IsNullOrWhiteSpace(_options.TidalClientId) &&
            !string.IsNullOrWhiteSpace(_options.TidalClientSecret) &&
            !string.IsNullOrWhiteSpace(_options.TidalCountryCode))
        {
            tidalTaggingSuccess = await _tidalService.WriteTagsAsync(mediaFileInfo, 
                mediaFileInfo.FileInfo, 
                ArtistHelper.GetUncoupledArtistName(mediaFileInfo.Artist), 
                ArtistHelper.GetUncoupledArtistName(mediaFileInfo.AlbumArtist),
                _options.OverwriteArtist, 
                _options.OverwriteAlbum, 
                _options.OverwriteTrack,
                _options.OverwriteAlbumArtist,
                _options.TidalMatchPercentage);
            
            if (tidalTaggingSuccess)
            {
                //read again the file after saving
                mediaFileInfo = new MediaFileInfo(mediaFileInfo.FileInfo);
                Logger.WriteLine($"Updated with Tidal tags {mediaFileInfo.FileInfo.FullName}", true);
            }
            else
            {
                Logger.WriteLine($"Tidal record not found by MediaTag information for {mediaFileInfo.FileInfo.FullName}", true);
            }
        }
        
        if (_options.OnlyMoveWhenTagged && !musicBrainzTaggingSuccess && !tidalTaggingSuccess && !metadataApiTaggingSuccess)
        {
            Logger.WriteLine($"Skipped processing, tagging failed for '{mediaFileInfo.FileInfo.FullName}'");
            return false;
        }
        
        string? oldArtistName = mediaFileInfo.AlbumArtist;
        string? artist = mediaFileInfo.AlbumArtist;
        bool updatedArtistName = false;

        if (string.IsNullOrWhiteSpace(artist) ||
            (artist.Contains(VariousArtistsName) && !string.IsNullOrWhiteSpace(mediaFileInfo.Artist)))
        {
            artist = mediaFileInfo.Artist;
        }
        
        if (string.IsNullOrWhiteSpace(artist) ||
            (artist.Contains(VariousArtistsName) && !string.IsNullOrWhiteSpace(mediaFileInfo.SortArtist)))
        {
            artist = mediaFileInfo.SortArtist;
        }

        if (string.IsNullOrWhiteSpace(artist) ||
            string.IsNullOrWhiteSpace(mediaFileInfo.Album) ||
            string.IsNullOrWhiteSpace(mediaFileInfo.Title))
        {
            Logger.WriteLine($"File is missing Artist, Album or title in the tags, skipping, {mediaFileInfo.FileInfo.FullName}");
            return false;
        }

        if (!String.IsNullOrWhiteSpace(_options.FileFormat))
        {
            string oldFileName = mediaFileInfo.FileInfo.Name;
            string newFileName = GetFormatName(mediaFileInfo, _options.FileFormat, _options.DirectorySeperator) + mediaFileInfo.FileInfo.Extension;
            string newFilePath = Path.Join(mediaFileInfo.FileInfo.Directory.FullName, newFileName);
            File.Move(mediaFileInfo.FileInfo.FullName, newFilePath, true);
            mediaFileInfo = new MediaFileInfo(new FileInfo(newFilePath));
            Logger.WriteLine($"Renamed '{oldFileName}' => '{newFileName}'", true);
        }

        DirectoryInfo toArtistDirInfo = new DirectoryInfo($"{_options.ToDirectory}{SanitizeArtistName(artist)}");
        DirectoryInfo toAlbumDirInfo = new DirectoryInfo($"{_options.ToDirectory}{SanitizeArtistName(artist)}/{SanitizeAlbumName(mediaFileInfo.Album)}");

        string? newArtistName = ArtistHelper.GetUncoupledArtistName(artist);

        if (!string.IsNullOrWhiteSpace(newArtistName) && newArtistName != artist)
        {
            updatedArtistName = true;
            if (!SetToArtistDirectory(newArtistName, mediaFileInfo, out toArtistDirInfo, out toAlbumDirInfo))
            {
                if (!_artistsNotFound.Contains(newArtistName))
                {
                    _artistsNotFound.Add(newArtistName);
                }
                
                Logger.WriteLine($"Skipped processing, Artist folder does not exist for '{mediaFileInfo.FileInfo.FullName}'");
                return false;
            }

            artist = newArtistName;
        }
        else
        {
            if (!SetToArtistDirectory(artist, mediaFileInfo, out toArtistDirInfo, out toAlbumDirInfo))
            {
                if (!_artistsNotFound.Contains(artist))
                {
                    _artistsNotFound.Add(artist);
                }
                Logger.WriteLine($"Skipped processing, Artist folder does not exist for '{mediaFileInfo.FileInfo.FullName}'");
                return false;
            }
        }

        if (!toArtistDirInfo.Exists)
        {
            Logger.WriteLine($"Skipped processing, Artist folder does not exist for '{mediaFileInfo.FileInfo.FullName}'");
            return false;
        }

        SimilarFileResult similarFileResult = await GetSimilarFileFromTagsArtistAsync(mediaFileInfo, mediaFileInfo.FileInfo, toAlbumDirInfo);

        if (similarFileResult.Errors && !_options.ContinueScanError)
        {
            Logger.WriteLine($"Scan errors... skipping {mediaFileInfo.FileInfo.FullName}", true);
            return false;
        }

        bool extraDirExists = true;
        foreach (string extraScaDir in _options.ExtraScans)
        {
            DirectoryInfo extraScaDirInfo = new DirectoryInfo(extraScaDir);
            string albumPath = GetDirectoryCaseInsensitive(extraScaDirInfo, $"{SanitizeArtistName(artist)}/{SanitizeAlbumName(mediaFileInfo.Album)}");
            DirectoryInfo albumDirInfo = new DirectoryInfo(Path.Join(extraScaDir, albumPath));

            if (!albumDirInfo.Exists)
            {
                extraDirExists = false;
                continue;
            }

            var extraSimilarResult = await GetSimilarFileFromTagsArtistAsync(mediaFileInfo, mediaFileInfo.FileInfo, albumDirInfo);

            if (extraSimilarResult.Errors && !_options.ContinueScanError)
            {
                break;
            }

            if (extraSimilarResult.SimilarFiles.Count > 0)
            {
                similarFileResult.SimilarFiles.AddRange(extraSimilarResult.SimilarFiles);
            }
        }

        if (!extraDirExists && _options.ExtraDirMustExist)
        {
            Logger.WriteLine($"Skipping file, artist '{artist}' does not exist in extra directory {mediaFileInfo.FileInfo.FullName}");
            return false;
        }

        if (similarFileResult.Errors && !_options.ContinueScanError)
        {
            Logger.WriteLine($"Scan errors... skipping {mediaFileInfo.FileInfo.FullName}", true);
            IncrementCounter(() => _skippedErrorFiles++);
            return false;
        }

        string fromFileName = mediaFileInfo.FileInfo.Name;

        if (_options.RenameVariousArtists &&
            fromFileName.Contains(VariousArtistsName))
        {
            fromFileName = fromFileName.Replace(VariousArtistsName, artist);
        }

        string newFromFilePath = $"{toAlbumDirInfo.FullName}/{fromFileName}";
        
        if (similarFileResult.SimilarFiles.Count == 0)
        {
            Logger.WriteLine($"No similar files found moving, {artist}/{mediaFileInfo.Album}, {newFromFilePath}", true);
            
            if (!toAlbumDirInfo.Exists)
            {
                toAlbumDirInfo.Create();
                IncrementCounter(() => _createdSubDirectories++);

                Logger.WriteLine($"Created directory, {toAlbumDirInfo.FullName}", true);
            }

            UpdateArtistTag(updatedArtistName, mediaFileInfo, oldArtistName, artist, mediaFileInfo.FileInfo);
            mediaFileInfo.FileInfo.MoveTo(newFromFilePath, true);
            Logger.WriteLine($"Moved {mediaFileInfo.FileInfo.Name} >> {newFromFilePath}", true);
            RemoveCacheByPath(newFromFilePath);

            IncrementCounter(() => _movedFiles++);
        }
        else if (similarFileResult.SimilarFiles.Count == 1)
        {
            var similarFile = similarFileResult.SimilarFiles.First();

            bool isFromPreferredQuality = _options.PreferredFileExtensions.Any(ext => mediaFileInfo.FileInfo.Extension.Contains(ext));
            bool isFromNonPreferredQuality = _options.NonPreferredFileExtensions.Any(ext => mediaFileInfo.FileInfo.Extension.Contains(ext));
            bool isSimilarPreferredQuality = _options.PreferredFileExtensions.Any(ext => similarFile.File.Extension.Contains(ext));
            bool isNonPreferredQuality = _options.NonPreferredFileExtensions.Any(ext => similarFile.File.Extension.Contains(ext));

            if (!isFromPreferredQuality && isSimilarPreferredQuality)
            {
                if (_options.DeleteDuplicateFrom)
                {
                    mediaFileInfo.FileInfo.Delete();
                    IncrementCounter(() => _localDelete++);
                    Logger.WriteLine($"Similar file found, deleted from file, quality is lower, {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}, {mediaFileInfo.FileInfo.FullName}");
                }
                else
                {
                    Logger.WriteLine($"Similar file found, quality is lower, {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}, {mediaFileInfo.FileInfo.FullName}");
                }
            }
            else if (isFromPreferredQuality && isNonPreferredQuality || //overwrite lower quality based on extension
                     (isFromPreferredQuality && isSimilarPreferredQuality && mediaFileInfo.FileInfo.Length > similarFile.File.Length) || //overwrite based on filesize, both high quality
                     (isFromNonPreferredQuality && isNonPreferredQuality && mediaFileInfo.FileInfo.Length > similarFile.File.Length)) //overwrite based on filesize, both low quality
            {
                if (!toAlbumDirInfo.Exists)
                {
                    toAlbumDirInfo.Create();
                    IncrementCounter(() => _createdSubDirectories++);
                    Logger.WriteLine($"Created directory, {toAlbumDirInfo.FullName}", true);
                }

                UpdateArtistTag(updatedArtistName, mediaFileInfo, oldArtistName, artist, mediaFileInfo.FileInfo);
                mediaFileInfo.FileInfo.MoveTo(newFromFilePath, true);
                Logger.WriteLine($"Moved {mediaFileInfo.FileInfo.Name} >> {newFromFilePath}", true);
                
                
                RemoveCacheByPath(newFromFilePath);
                RemoveCacheByPath(similarFile.File.FullName);

                if (similarFile.File.FullName != newFromFilePath && _options.DeleteDuplicateTo)
                {
                    similarFile.File.Delete();
                    IncrementCounter(() => _remoteDelete++);
                    Logger.WriteLine($"Deleting duplicated file '{similarFile.File.FullName}'");
                }

                IncrementCounter(() => _movedFiles++);

                Logger.WriteLine($"Similar file found, overwriting target, From is bigger, {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}, {mediaFileInfo.FileInfo.FullName}", true);
            }
            else if (similarFile.File.Length == mediaFileInfo.FileInfo.Length)
            {
                if (_options.DeleteDuplicateFrom)
                {
                    mediaFileInfo.FileInfo.Delete();
                    IncrementCounter(() => _localDelete++);
                    Logger.WriteLine($"Similar file found, deleted from file, exact same size from/target, {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}, {mediaFileInfo.FileInfo.FullName}", true);
                }
            }
            else if (similarFile.File.Length > mediaFileInfo.FileInfo.Length)
            {
                if (_options.DeleteDuplicateFrom)
                {
                    mediaFileInfo.FileInfo.Delete();
                    IncrementCounter(() => _localDelete++);
                    Logger.WriteLine($"Similar file found, deleted from file, Target is bigger, {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}, {mediaFileInfo.FileInfo.FullName}", true);
                }
            }
            else
            {
                Logger.WriteLine($"[To Be Implemented] Similar file found {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}, {similarFile.File.Extension}");
            }
        }
        else if (similarFileResult.SimilarFiles.Count >= 2)
        {
            bool isFromPreferredQuality = _options.PreferredFileExtensions.Any(ext => mediaFileInfo.FileInfo.Extension.Contains(ext));
            bool isNonPreferredQuality = _options.NonPreferredFileExtensions.Any(ext => similarFileResult.SimilarFiles.Any(similarFile => similarFile.File.Extension.Contains(ext)));
            
            if (isFromPreferredQuality && isNonPreferredQuality)
            {
                if (!toAlbumDirInfo.Exists)
                {
                    toAlbumDirInfo.Create();
                    IncrementCounter(() => _createdSubDirectories++);
                    Logger.WriteLine($"Created directory, {toAlbumDirInfo.FullName}", true);
                }

                UpdateArtistTag(updatedArtistName, mediaFileInfo, oldArtistName, artist, mediaFileInfo.FileInfo);
                mediaFileInfo.FileInfo.MoveTo(newFromFilePath, true);
                Logger.WriteLine($"Moved {mediaFileInfo.FileInfo.Name} >> {newFromFilePath}");
                RemoveCacheByPath(newFromFilePath);

                if (_options.DeleteDuplicateTo)
                {
                    similarFileResult.SimilarFiles.ForEach(similarFile =>
                    {
                        if (similarFile.File.FullName != newFromFilePath)
                        {
                            Logger.WriteLine($"Deleting duplicated file '{similarFile.File.FullName}'");
                            RemoveCacheByPath(similarFile.File.FullName);
                            similarFile.File.Delete();
                            IncrementCounter(() => _remoteDelete++);
                        }
                    });
                }

                IncrementCounter(() => _movedFiles++);
                Logger.WriteLine($"Similar files found, overwriting target, From is bigger, {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}, {mediaFileInfo.FileInfo.FullName}");
            }
            else if(_options.DeleteDuplicateFrom)
            {
                mediaFileInfo.FileInfo.Delete();
                IncrementCounter(() => _localDelete++);
                Logger.WriteLine($"Similar files found, deleted from file, Targets are bigger, {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}, {mediaFileInfo.FileInfo.FullName}");
            }

            Logger.WriteLine($"Similar files found {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}");
        }

        return true;
    }

    private void RemoveCacheByPath(string fullPath)
    {
        lock (_memoryCache)
        {
            var cachedMediaInfo = _memoryCache.Get(fullPath) as MediaFileInfo;
            if (cachedMediaInfo != null)
            {
                _memoryCache.Remove(fullPath, CacheEntryRemovedReason.Removed);
            }
        }
    }

    private async Task<SimilarFileResult> GetSimilarFileFromTagsArtistAsync(
        MediaFileInfo matchTagFile, 
        FileInfo fromFileInfo,
        DirectoryInfo toAlbumDirInfo)
    {
        SimilarFileResult similarFileResult = new SimilarFileResult();

        if (!toAlbumDirInfo.Exists)
        {
            return similarFileResult;
        }
        
        List<FileInfo> toFiles = toAlbumDirInfo
            .GetFiles("*.*", SearchOption.AllDirectories)
            .Where(file => file.Length > 0)
            .Where(file => MediaFileExtensions.Any(ext => file.Name.ToLower().EndsWith(ext)))
            .ToList();

        foreach (FileInfo toFile in toFiles)
        {
            try
            {
                if (toFile.FullName == fromFileInfo.FullName)
                {
                    continue;
                }
                
                //quick compare before caching/reading the file tags, if filename matches +95%
                if (Fuzz.Ratio(toFile.Name.ToLower().Replace(toFile.Extension, string.Empty),
                        fromFileInfo.Name.ToLower().Replace(fromFileInfo.Extension, string.Empty)) >= 95)
                {
                    similarFileResult.SimilarFiles.Add(new SimilarFileInfo(toFile));
                    continue;
                }

                if (_options.OnlyFileNameMatching)
                {
                    continue;
                }
                
                MediaFileInfo? cachedMediaInfo = null;

                lock (_memoryCache)
                {
                    cachedMediaInfo = _memoryCache.Get(toFile.FullName) as MediaFileInfo;
                }

                if (cachedMediaInfo == null)
                {
                    cachedMediaInfo = await AddFileToCacheAsync(toFile);
                }
                else
                {
                    IncrementCounter(() => _cachedReadTargetFiles++);
                }
                
                if (cachedMediaInfo == null)
                {
                    similarFileResult.Errors = true;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(cachedMediaInfo.Title) || 
                    string.IsNullOrWhiteSpace(cachedMediaInfo.Album) || 
                    string.IsNullOrWhiteSpace(cachedMediaInfo.Artist) || 
                    string.IsNullOrWhiteSpace(cachedMediaInfo.AlbumArtist))
                {
                    Logger.WriteLine($"scan error file, no Title, Album, Artist or AlbumArtist: {toFile}");
                    similarFileResult.Errors = true;
                    continue;
                }

                bool artistMatch = Fuzz.Ratio(cachedMediaInfo?.Artist,  matchTagFile.Artist) >= NamingAccuracy ||
                                   Fuzz.Ratio(ArtistHelper.GetUncoupledArtistName(cachedMediaInfo?.Artist), ArtistHelper.GetUncoupledArtistName(matchTagFile.Artist)) >= NamingAccuracy;
                
                bool albumArtistMatch = Fuzz.Ratio(cachedMediaInfo?.AlbumArtist, matchTagFile.AlbumArtist) >= NamingAccuracy ||
                                        Fuzz.Ratio(ArtistHelper.GetUncoupledArtistName(cachedMediaInfo?.AlbumArtist), ArtistHelper.GetUncoupledArtistName(matchTagFile.AlbumArtist)) >= NamingAccuracy;
                
                if (Fuzz.Ratio(cachedMediaInfo?.Title, matchTagFile.Title) >= NamingAccuracy &&
                    Fuzz.Ratio(cachedMediaInfo?.Album,  matchTagFile.Album) >= NamingAccuracy &&
                    cachedMediaInfo?.Track == matchTagFile.Track &&
                    artistMatch && albumArtistMatch)
                {
                    similarFileResult.SimilarFiles.Add(new SimilarFileInfo(toFile, cachedMediaInfo));
                }
                //very tricky based on fingerprint
                //songs can be both in e.g. "Best of ..." and in the actual album
                //else if (!string.IsNullOrWhiteSpace(cachedMediaInfo?.AcoustIdFingerPrint) && 
                //         !string.IsNullOrWhiteSpace(matchTagFile.AcoustIdFingerPrint) &&
                //         cachedMediaInfo?.AcoustIdFingerPrint == matchTagFile.AcoustIdFingerPrint)
                //{
                //    similarFiles.Add(new SimilarFileInfo(toFile, cachedMediaInfo));
                //}
            }
            catch (Exception e)
            {
                similarFileResult.Errors = true;
                Logger.WriteLine($"scan error file, {e.Message}, {toFile.FullName}");
            }
        }

        return similarFileResult;
    }

    private async Task<MediaFileInfo?> AddFileToCacheAsync(FileInfo fileInfo)
    {
        MediaFileInfo? cachedMediaInfo = null;
        IncrementCounter(() => _scannedTargetFiles++);

        try
        {
            cachedMediaInfo = new MediaFileInfo(fileInfo);
        }
        catch (Exception e)
        {
            Logger.WriteLine($"File gave read error, '{fileInfo.FullName}', {e.Message}");
            return null;
        }

        try
        {
            if (cachedMediaInfo is null &&
                _options.FixFileCorruption &&
                await _corruptionFixer.FixCorruptionAsync(fileInfo))
            {
                cachedMediaInfo = new MediaFileInfo(fileInfo);
                IncrementCounter(() => _fixedCorruptedFiles++);
            }
        }
        catch (Exception e)
        {
            Logger.WriteLine($"File gave read error, '{fileInfo.FullName}', {e.Message}");
            return null;
        }

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
    
    private void UpdateArtistTag(bool updatedArtistName, MediaFileInfo tagFile, string oldArtistName, string artist,
        FileInfo fromFile)
    {
        if (updatedArtistName && _options.UpdateArtistTags)
        {
            tagFile.Save(artist);
            
            IncrementCounter(() => _updatedTagfiles++);
        }
    }

    private string SanitizeAlbumName(string albumName)
    {
        return albumName
            .Replace('/', '+')
            .Replace('\\', '+');
    }

    private string SanitizeArtistName(string artistName)
    {
        return artistName
            .Replace('/', '+')
            .Replace('\\', '+');
    }

    private void ShowProgress()
    {
        Logger.WriteLine($"Stats: Moved {_movedFiles}, " +
                              $"Local Delete: {_localDelete}, " +
                              $"Remote Delete: {_remoteDelete}, " +
                              $"Fixed Corrupted: {_fixedCorruptedFiles}, " +
                              $"Updated Artist Tags: {_updatedTagfiles}, " +
                              $"Created SubDirectories: {_createdSubDirectories}, " +
                              $"Scanned From: {_scannedFromFiles}. " +
                              $"Cached Read Target: {_cachedReadTargetFiles}. " +
                              $"Scanned Target: {_scannedTargetFiles}. " +
                              $"Skipped Error: {_skippedErrorFiles}, " +
                              $"Running: {(int)_runtimeSw.Elapsed.TotalMinutes}:{_runtimeSw.Elapsed.Seconds}");
    }

    private void IncrementCounter(Action callback)
    {
        lock (_counterLock)
        {
            callback();
        }
    }

    private bool EnoughDiskSpace()
    {
        DriveInfo drive = new DriveInfo(_options.ToDirectory);

        if (!drive.IsReady)
        {
            return false;
        }

        return drive.AvailableFreeSpace > MinAvailableDiskSpace * (1024 * 1024);
    }
    
    public string GetFormatName(MediaFileInfo file, string format,  string seperator)
    {
        file.Artist = ReplaceDirectorySeparators(file.Artist, seperator);
        file.Title = ReplaceDirectorySeparators(file.Title, seperator);
        file.Album = ReplaceDirectorySeparators(file.Album, seperator);
        format = Smart.Format(format, file);
        format = format.Trim();
        return format;
    }
    private string ReplaceDirectorySeparators(string? input, string seperator)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }
        
        if (input.Contains('/'))
        {
            input = input.Replace("/", seperator);
        }
        else if (input.Contains('\\'))
        {
            input = input.Replace("\\", seperator);
        }

        return input;
    }
    
    
    private string GetDirectoryCaseInsensitive(DirectoryInfo directory, string directoryPath)
    {
        DirectoryInfo tempDirectory = directory;
        string[] subDirectories = directoryPath.Split(Path.DirectorySeparatorChar);
        List<string> newSubDirectoryNames = new List<string>();
        foreach (string subDirectory in subDirectories)
        {
            if (tempDirectory == null)
            {
                newSubDirectoryNames.Add(subDirectory);
                continue;
            }
            string dirName = GetNextDirectoryCaseInsensitive(tempDirectory, subDirectory, out tempDirectory);
            newSubDirectoryNames.Add(dirName);
        }
        
        return string.Join(Path.DirectorySeparatorChar, newSubDirectoryNames);
    }

    private string GetNextDirectoryCaseInsensitive(DirectoryInfo directory, string subDirectory, out DirectoryInfo? targetDir)
    {
        targetDir = directory.GetDirectories()
            .OrderBy(dir => dir.Name)
            .FirstOrDefault(dir => string.Equals(dir.Name, subDirectory, StringComparison.OrdinalIgnoreCase));

        if (targetDir != null)
        {
            return targetDir.Name;
        }
        return subDirectory;
    }
}