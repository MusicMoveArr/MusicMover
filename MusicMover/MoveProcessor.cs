using System.Diagnostics;
using System.Runtime.Caching;
using System.Text.RegularExpressions;
using FuzzySharp;
using MusicMover.Helpers;
using MusicMover.MediaHandlers;
using MusicMover.Models;
using MusicMover.Services;
using MusicMoverPlugin;
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
    
    public const string MediaHandlerFFmpeg = "FFmpeg";
    public const string MediaHandlerATLCore = "ATL_Core";

    private const long MinAvailableDiskSpace = 5000; //GB
    private const int CacheTime = 5; //minutes
    private const string VariousArtistsName = "Various Artists";

    private const int NamingAccuracy = 98;
    private const int MaxFilePartNameLength = 50;
    
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
    private int _processedFiles = 0;

    private Stopwatch _sw = Stopwatch.StartNew();
    private Stopwatch _runtimeSw = Stopwatch.StartNew();
    private List<MediaHandler> _filesToProcess = new List<MediaHandler>();
    private const int BatchFileProcessing = 1000;

    private readonly CliOptions _options;
    private readonly MemoryCache _memoryCache;
    private readonly CorruptionFixer _corruptionFixer;
    private readonly MusicBrainzService _musicBrainzService;
    private readonly TidalService _tidalService;
    private readonly MiniMediaMetadataService _miniMediaMetadataService;
    private readonly MediaTagWriteService _mediaTagWriteService;
    private readonly List<IPlugin> _plugins = new List<IPlugin>();
    private List<string> _artistsNotFound = new List<string>();
    private List<string> _unprocessedArtists = new List<string>();
    private readonly TranslationService _translationService;

    public MoveProcessor(CliOptions options)
    {
        _options = options;
        _memoryCache = MemoryCache.Default;
        _corruptionFixer = new CorruptionFixer();
        _musicBrainzService = new MusicBrainzService();
        _mediaTagWriteService = new MediaTagWriteService();
        _translationService = new TranslationService(options.TranslationPath);
        _tidalService = new TidalService(options.TidalClientId, options.TidalClientSecret, options.TidalCountryCode);
        _miniMediaMetadataService = new MiniMediaMetadataService(options.MetadataApiBaseUrl, options.MetadataApiProviders, _translationService);
    }

    public void LoadPlugins()
    {
        _plugins.AddRange(PluginHelper.LoadPlugins());
    }

    public async Task ProcessAsync()
    {
        await _translationService.LoadTranslationsAsync();
        
        if (!string.IsNullOrWhiteSpace(_options.FromDirectory))
        {
            var topDirectories = Directory
                .EnumerateFileSystemEntries(_options.FromDirectory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(file => Directory.Exists(file))
                .Where(dir => !dir.Contains(".Trash"))
                .Skip(_options.SkipFromDirAmount)
                .OrderBy(dir => dir)
                .ToList();
        
            await GetMediaFileListAsync(_options.FromDirectory, SearchOption.TopDirectoryOnly);

            int directoriesRead = 0;
            foreach (string directoryPath in topDirectories)
            {
                Logger.WriteLine($"[{directoriesRead++}/{topDirectories.Count}] Reading files from '{directoryPath}'");
                await GetMediaFileListAsync(directoryPath, SearchOption.AllDirectories);
            }
        }

        if (!string.IsNullOrWhiteSpace(_options.FromFile))
        {
            await GetMediaFileListFromFileAsync(_options.FromFile);
        }
        
        await ProcessMediaFiles();

        ShowProgress();

        if (_artistsNotFound.Count > 0)
        {
            Logger.WriteLine($"Artists not found: {_artistsNotFound.Count}");
            _artistsNotFound.ForEach(artist => Logger.WriteLine(artist));
        }

        if (_unprocessedArtists.Any())
        {
            Console.WriteLine("Unable to tag the following artists:");
            _unprocessedArtists
                .OrderBy(artist => artist)
                .ToList()
                .ForEach(artist => Logger.WriteLine(artist));
        }
    }

    private async Task ProcessMediaFiles()
    {
        _filesToProcess = _filesToProcess
            .OrderBy(file => file.Artist)
            .ThenBy(file => file.AlbumArtist)
            .ThenBy(file => file.Album)
            .ToList();
        
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
                var totalProgressTask = ctx.AddTask(Markup.Escape($"Processing tracks {_processedFiles} of {_filesToProcess.Count + _processedFiles}"));
                totalProgressTask.MaxValue = _filesToProcess.Count;
                
                int maxDegreeOfParallelism = _options.Parallel ? 5 : 1;
                AsyncLock progressLock = new AsyncLock();

                await ParallelHelper.ForEachAsync(_filesToProcess, maxDegreeOfParallelism, async mediaHandler =>
                {
                    string artistName = ArtistHelper.GetShortVersion(mediaHandler.Artist, 30, "...");
                    string albumName = ArtistHelper.GetShortVersion(mediaHandler.Album, 30, "...");
                    string trackName = ArtistHelper.GetShortVersion(mediaHandler.Title, 30, "...");
                    
                    using (await progressLock.LockAsync())
                    {
                        if (!progressTasks.ContainsKey(artistName))
                        {
                            int trackCount = _filesToProcess.Count(file => string.Equals(file.Artist, mediaHandler.Artist));
                            var task = ctx.AddTask(Markup.Escape($"Processing Artist '{artistName}' 0 of {trackCount} processed, Album: '{albumName}', Track: '{trackName}'"));
                            task.MaxValue = trackCount;
                            progressTasks.TryAdd(artistName, task);
                        }
                    }
                    
                    try
                    {
                        await ProcessFromFileAsync(mediaHandler);
                    }
                    catch (Exception e)
                    {
                        Logger.WriteLine($"{mediaHandler.FileInfo.FullName}, {e.Message}. \r\n{e.StackTrace}");
                        IncrementCounter(() => _skippedErrorFiles++);
                    }
                    
                    using (await progressLock.LockAsync())
                    {
                        if (!mediaHandler.TaggerUpdatedTags)
                        {
                            foreach (string artist in mediaHandler.AllArtistNames)
                            {
                                if(!_unprocessedArtists.Contains(artist))
                                {
                                    _unprocessedArtists.Add(artist);
                                }
                            }
                        }
                        
                        
                        if (progressTasks.TryGetValue(artistName, out ProgressTask progressTask))
                        {
                            progressTask.Increment(1);
                            progressTask.Description(Markup.Escape($"Processing Artist '{artistName}' {progressTask.Value} of {progressTask.MaxValue} processed, Album: '{albumName}', Track: '{trackName}'"));
                        }
                    }
                    
                    totalProgressTask.Value++;
                    totalProgressTask.Description(Markup.Escape($"Processing tracks {totalProgressTask.Value + _processedFiles} of {_filesToProcess.Count + _processedFiles}, moved: {_movedFiles}, local delete: {_localDelete}, remote delete: {_remoteDelete}"));
                });
            });

        _processedFiles += _filesToProcess.Count;
        _filesToProcess.Clear();
    }

    private async Task GetMediaFileListAsync(string fromTopDir, SearchOption dirSearchOption)
    {
        DirectoryInfo fromDirInfo = new DirectoryInfo(fromTopDir);

        FileInfo[] fromFiles = fromDirInfo
            .GetFiles("*.*", dirSearchOption)
            .Where(file => file.Length > 0)
            .Where(file => MediaFileExtensions.Any(ext => file.Name.ToLower().EndsWith(ext)))
            .ToArray();

        foreach (FileInfo fromFile in fromFiles)
        {
            MediaHandler? mediaFileInfo = await GetMediaFileHandlerAsync(fromFile);

            if (mediaFileInfo != null)
            {
                _filesToProcess.Add(mediaFileInfo);

                if (_filesToProcess.Count >= BatchFileProcessing)
                {
                    await ProcessMediaFiles();
                }
            }
        }
    }

    private async Task GetMediaFileListFromFileAsync(string fromFilePath)
    {
        using TextReader textReader = new StreamReader(fromFilePath);
        string? filePath = string.Empty;
        
        while(!string.IsNullOrWhiteSpace(filePath = await textReader.ReadLineAsync()))
        {
            FileInfo fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                continue;
            }
            Logger.WriteLine($"Reading file '{fileInfo.FullName}'");
            MediaHandler? mediaHandler = await GetMediaFileHandlerAsync(fileInfo);

            if (mediaHandler != null)
            {
                _filesToProcess.Add(mediaHandler);

                if (_filesToProcess.Count >= BatchFileProcessing)
                {
                    await ProcessMediaFiles();
                }
            }
        }
    }

    private async Task<MediaHandler?> GetMediaFileHandlerAsync(FileInfo fromFile)
    {
        MediaHandler mediaHandler = null;
            
        try
        {
            switch (_options.MetadataHandlerLibrary)
            {
                case MediaHandlerATLCore:
                    mediaHandler = new MediaHandlerAtlCore(fromFile);
                    break;
                case MediaHandlerFFmpeg:
                    mediaHandler = new MediaHandlerFFmpeg(fromFile);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"{ex.Message}, {fromFile.FullName}");
            IncrementCounter(() => _skippedErrorFiles++);
        }

        try
        {
            if (mediaHandler is null &&
                _options.FixFileCorruption &&
                await _corruptionFixer.FixCorruptionAsync(fromFile))
            {
                switch (_options.MetadataHandlerLibrary)
                {
                    case MediaHandlerATLCore:
                        mediaHandler = new MediaHandlerAtlCore(fromFile);
                        break;
                    case MediaHandlerFFmpeg:
                        mediaHandler = new MediaHandlerFFmpeg(fromFile);
                        break;
                }
                IncrementCounter(() => _fixedCorruptedFiles++);
            }
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"{ex.Message}, {fromFile.FullName}");
            IncrementCounter(() => _skippedErrorFiles++);
        }
        return mediaHandler;
    }

    private bool SetToArtistDirectory(
        string artist, 
        string album,
        out DirectoryInfo toArtistDirInfo,
        out DirectoryInfo toAlbumDirInfo)
    {
        DirectoryInfo musicDirInfo = new DirectoryInfo(_options.ToDirectory);
        string artistPath = GetDirectoryCaseInsensitive(musicDirInfo,SanitizeArtistName(artist));
        string albumPath = GetDirectoryCaseInsensitive(musicDirInfo, $"{SanitizeArtistName(artist)}/{SanitizeAlbumName(album)}");
        
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

    private async Task<bool> ProcessFromFileAsync(MediaHandler mediaHandler)
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

        if (!mediaHandler.FileInfo.Exists)
        {
            Logger.WriteLine($"Media file no longer exists '{mediaHandler.FileInfo.FullName}'");
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
        Debug.WriteLine($"File: {mediaHandler.FileInfo.FullName}");

        foreach (var plugin in _plugins)
        { 
            //plugin.OnLoad(mediaHandler);
        }

        await TagFileAcoustIdAsync(mediaHandler);
        await TagFileMetadataApiAsync(mediaHandler);
        await TagFileTidalAsync(mediaHandler);

        if (!mediaHandler.MusicBrainzTaggingSuccess &&
            !mediaHandler.TidalTaggingSuccess &&
            !mediaHandler.MetadataApiTaggingSuccess)
        {
            string oldArtist = mediaHandler.Artist ?? string.Empty;
            string oldTitle = mediaHandler.Title ?? string.Empty;
            
            //remove numbers at the start of the title
            mediaHandler.SetMediaTagValue("Title", Regex.Replace(mediaHandler.Title, @"^[0-9.\- ]*", string.Empty).TrimStart());
            //remove (Album Version)
            mediaHandler.SetMediaTagValue("Title", mediaHandler.Title.Replace("(Album Version)", string.Empty, StringComparison.OrdinalIgnoreCase));
            
            //remove the artist name at the start of the title
            //if artist name is empty, fill artist name partly from the title
            var artistMatches = Regex.Matches(mediaHandler.Title, @"^([\d\w ]{3,})\-");
            if (artistMatches.Count > 0)
            {
                mediaHandler.SetMediaTagValue("Title", Regex.Replace(mediaHandler.Title, @"^([\d\w ]{3,})\-", string.Empty).TrimStart());
                
                if (string.IsNullOrWhiteSpace(mediaHandler.Artist))
                {
                    mediaHandler.SetMediaTagValue("Artist", artistMatches.First().Groups[1].Value);
                }
            }

            mediaHandler.SetMediaTagValue("Title", mediaHandler.Title.Trim());
            mediaHandler.SetMediaTagValue("Artist", mediaHandler.Artist.Trim());

            if (!string.Equals(oldArtist, mediaHandler.Artist) ||
                !string.Equals(oldTitle, mediaHandler.Title))
            {
                await TagFileAcoustIdAsync(mediaHandler);
                await TagFileMetadataApiAsync(mediaHandler);
                await TagFileTidalAsync(mediaHandler);
            }
        }

        if (_options.OnlyMoveWhenTagged && 
            !mediaHandler.MusicBrainzTaggingSuccess && 
            !mediaHandler.TidalTaggingSuccess &&
            !mediaHandler.MetadataApiTaggingSuccess &&
            _options.TrustAcoustIdWhenTaggingFailed &&
            !string.IsNullOrWhiteSpace(_options.AcoustIdApiKey))
        {
            //empty the tags we use for tagging and try again
            mediaHandler.SetMediaTagValue(string.Empty, "Album");
            mediaHandler.SetMediaTagValue(string.Empty, "AlbumArtist");
            mediaHandler.SetMediaTagValue(string.Empty, "Artist");
            mediaHandler.SetMediaTagValue(string.Empty, "Title");
            
            await TagFileAcoustIdAsync(mediaHandler);
            await TagFileMetadataApiAsync(mediaHandler);
            await TagFileTidalAsync(mediaHandler);
        }

        if (mediaHandler.MusicBrainzTaggingSuccess || 
            mediaHandler.TidalTaggingSuccess || 
            mediaHandler.MetadataApiTaggingSuccess)
        {
            foreach (var plugin in _plugins)
            {
                //plugin.AfterTagging(mediaHandler);
            }
        }
        
        if (_options.OnlyMoveWhenTagged && 
            !mediaHandler.MusicBrainzTaggingSuccess && 
            !mediaHandler.TidalTaggingSuccess && 
            !mediaHandler.MetadataApiTaggingSuccess)
        {
            foreach (var plugin in _plugins)
            {
                //plugin.TaggingFailed(mediaHandler);
            }
            Logger.WriteLine($"Skipped processing, tagging failed for '{mediaHandler.FileInfo.FullName}'");

            if (!string.IsNullOrWhiteSpace(_options.MoveUntaggableFilesPath))
            {
                string artistFolderName = SanitizeArtistName(mediaHandler.Artist);
                string albumFolderName = SanitizeAlbumName(mediaHandler.Album);
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
                string safeFileName = string.Join("_", mediaHandler.FileInfo.Name.Split(Path.GetInvalidFileNameChars()));
                
                string newFilePath = Path.Join(
                    _options.MoveUntaggableFilesPath, 
                    artistFolderName, 
                    albumFolderName, 
                    safeFileName);
                Logger.WriteLine($"Moving untaggable file '{mediaHandler.FileInfo.FullName}' >> '{newFilePath}'");
                FileInfo newFilePathInfo = new FileInfo(newFilePath);
                if (!newFilePathInfo.Directory.Exists)
                {
                    newFilePathInfo.Directory.Create();
                }
                File.Move(mediaHandler.FileInfo.FullName, newFilePath, true);
            }
            
            return false;
        }
        
        
        string? artist = mediaHandler.AlbumArtist;
        bool updatedArtistName = false;

        if (string.IsNullOrWhiteSpace(artist) ||
            (artist.Contains(VariousArtistsName) && !string.IsNullOrWhiteSpace(mediaHandler.Artist)))
        {
            artist = mediaHandler.Artist;
        }
        
        if (string.IsNullOrWhiteSpace(artist) ||
            (artist.Contains(VariousArtistsName) && !string.IsNullOrWhiteSpace(mediaHandler.SortArtist)))
        {
            artist = mediaHandler.SortArtist;
        }

        if (string.IsNullOrWhiteSpace(artist) ||
            string.IsNullOrWhiteSpace(mediaHandler.Album) ||
            string.IsNullOrWhiteSpace(mediaHandler.Title))
        {
            Logger.WriteLine($"File is missing Artist, Album or title in the tags, skipping, {mediaHandler.FileInfo.FullName}");
            return false;
        }

        if (!String.IsNullOrWhiteSpace(_options.FileFormat))
        {
            string newFileName = SanitizeFileName(GetFormatName(mediaHandler, _options.FileFormat, _options.DirectorySeperator)) + mediaHandler.FileInfo.Extension;
            string newFilePath = Path.Join(mediaHandler.FileInfo.Directory.FullName, newFileName);
            mediaHandler.TargetSaveFileInfo = new FileInfo(newFilePath);
        }
        
        

        DirectoryInfo toArtistDirInfo = new DirectoryInfo($"{_options.ToDirectory}{SanitizeArtistName(artist)}");
        DirectoryInfo toAlbumDirInfo = new DirectoryInfo($"{_options.ToDirectory}{SanitizeArtistName(artist)}/{SanitizeAlbumName(mediaHandler.Album)}");

        string? newArtistName = ArtistHelper.GetUncoupledArtistName(artist);

        if (!string.IsNullOrWhiteSpace(newArtistName) && newArtistName != artist)
        {
            updatedArtistName = true;
            if (!SetToArtistDirectory(newArtistName, mediaHandler.Album, out toArtistDirInfo, out toAlbumDirInfo))
            {
                if (!_artistsNotFound.Contains(newArtistName))
                {
                    _artistsNotFound.Add(newArtistName);
                }
                
                Logger.WriteLine($"Skipped processing, Artist folder does not exist for '{mediaHandler.FileInfo.FullName}'");
                return false;
            }

            artist = newArtistName;
        }
        else
        {
            if (!SetToArtistDirectory(artist, mediaHandler.Album, out toArtistDirInfo, out toAlbumDirInfo))
            {
                if (!_artistsNotFound.Contains(artist))
                {
                    _artistsNotFound.Add(artist);
                }
                Logger.WriteLine($"Skipped processing, Artist folder does not exist for '{mediaHandler.FileInfo.FullName}'");
                return false;
            }
        }

        if (!toArtistDirInfo.Exists)
        {
            Logger.WriteLine($"Skipped processing, Artist folder does not exist for '{mediaHandler.FileInfo.FullName}'");
            return false;
        }

        mediaHandler.SimilarFileResult = await GetSimilarFileFromTagsArtistAsync(mediaHandler, mediaHandler.TargetSaveFileInfo, toAlbumDirInfo);

        if (mediaHandler.SimilarFileResult.Errors && !_options.ContinueScanError)
        {
            Logger.WriteLine($"Scan errors... skipping {mediaHandler.FileInfo.FullName}", true);
            return false;
        }

        bool extraDirExists = true;
        foreach (string extraScaDir in _options.ExtraScans)
        {
            DirectoryInfo extraScaDirInfo = new DirectoryInfo(extraScaDir);
            string albumPath = GetDirectoryCaseInsensitive(extraScaDirInfo, $"{SanitizeArtistName(artist)}/{SanitizeAlbumName(mediaHandler.Album)}");
            DirectoryInfo albumDirInfo = new DirectoryInfo(Path.Join(extraScaDir, albumPath));

            if (!albumDirInfo.Exists)
            {
                extraDirExists = false;
                continue;
            }

            var extraSimilarResult = await GetSimilarFileFromTagsArtistAsync(mediaHandler, mediaHandler.TargetSaveFileInfo, albumDirInfo);

            if (extraSimilarResult.Errors && !_options.ContinueScanError)
            {
                break;
            }

            if (extraSimilarResult.SimilarFiles.Count > 0)
            {
                mediaHandler.SimilarFileResult.SimilarFiles.AddRange(extraSimilarResult.SimilarFiles);
            }
        }

        if (!extraDirExists && _options.ExtraDirMustExist)
        {
            Logger.WriteLine($"Skipping file, artist '{artist}' does not exist in extra directory {mediaHandler.FileInfo.FullName}");
            return false;
        }

        if (mediaHandler.SimilarFileResult.Errors && !_options.ContinueScanError)
        {
            Logger.WriteLine($"Scan errors... skipping {mediaHandler.FileInfo.FullName}", true);
            IncrementCounter(() => _skippedErrorFiles++);
            return false;
        }

        string fromFileName = mediaHandler.TargetSaveFileInfo.Name;

        if (_options.RenameVariousArtists &&
            fromFileName.Contains(VariousArtistsName))
        {
            fromFileName = fromFileName.Replace(VariousArtistsName, artist);
        }

        if (_options.IsDryRun)
        {
            return false;
        }

        string newFromFilePath = $"{toAlbumDirInfo.FullName}/{fromFileName}";
        
        if (mediaHandler.SimilarFileResult.SimilarFiles.Count == 0)
        {
            Logger.WriteLine($"No similar files found moving, {artist}/{mediaHandler.Album}, {newFromFilePath}", true);
            
            if (!toAlbumDirInfo.Exists)
            {
                toAlbumDirInfo.Create();
                IncrementCounter(() => _createdSubDirectories++);

                Logger.WriteLine($"Created directory, {toAlbumDirInfo.FullName}", true);
            }

            await UpdateArtistTagAsync(updatedArtistName, mediaHandler, artist);

            await mediaHandler.GenerateSaveFingerprintAsync();

            bool saveSuccess = false;
            
            if ((saveSuccess = await _mediaTagWriteService.SafeSaveAsync(mediaHandler, new FileInfo(newFromFilePath))) &&
                !string.Equals(mediaHandler.FileInfo.FullName, newFromFilePath))
            {
                DumpCoverArt(mediaHandler, toAlbumDirInfo);
                mediaHandler.FileInfo.Delete();
            }

            if (saveSuccess)
            {
                Logger.WriteLine($"Moved {mediaHandler.FileInfo.Name} >> {newFromFilePath}", true);
                IncrementCounter(() => _movedFiles++);
            }
            RemoveCacheByPath(newFromFilePath);
        }
        else if (mediaHandler.SimilarFileResult.SimilarFiles.Count == 1)
        {
            var similarFile = mediaHandler.SimilarFileResult.SimilarFiles.First();

            bool isFromPreferredQuality = _options.PreferredFileExtensions.Any(ext => mediaHandler.FileInfo.Extension.Contains(ext));
            bool isFromNonPreferredQuality = _options.NonPreferredFileExtensions.Any(ext => mediaHandler.FileInfo.Extension.Contains(ext));
            bool isSimilarPreferredQuality = _options.PreferredFileExtensions.Any(ext => similarFile.File.Extension.Contains(ext));
            bool isNonPreferredQuality = _options.NonPreferredFileExtensions.Any(ext => similarFile.File.Extension.Contains(ext));
            bool inputIsOutput = string.Equals(similarFile.File.FullName, mediaHandler.FileInfo.FullName);

            if (inputIsOutput)
            {
                if (!toAlbumDirInfo.Exists)
                {
                    toAlbumDirInfo.Create();
                    IncrementCounter(() => _createdSubDirectories++);
                    Logger.WriteLine($"Created directory, {toAlbumDirInfo.FullName}", true);
                }

                await UpdateArtistTagAsync(updatedArtistName, mediaHandler, artist);

                await mediaHandler.GenerateSaveFingerprintAsync();

                if (await _mediaTagWriteService.SafeSaveAsync(mediaHandler, new FileInfo(newFromFilePath)))
                {
                    DumpCoverArt(mediaHandler, toAlbumDirInfo);
                    if (!string.Equals(mediaHandler.FileInfo.FullName, newFromFilePath))
                    {
                        mediaHandler.FileInfo.Delete();
                    }
                    
                    Logger.WriteLine($"Updated {mediaHandler.FileInfo.Name} >> {newFromFilePath}", true);
                    RemoveCacheByPath(similarFile.File.FullName);
                    Logger.WriteLine($"Similar file found, overwritten input file, {mediaHandler.SimilarFileResult.SimilarFiles.Count}, {artist}/{mediaHandler.Album}, {mediaHandler.FileInfo.FullName}", true);
                }
                else
                {
                    Logger.WriteLine("Failed to update/write to the file...??");
                }
            }
            else if (!isFromPreferredQuality && isSimilarPreferredQuality)
            {
                if (_options.DeleteDuplicateFrom)
                {
                    mediaHandler.FileInfo.Delete();
                    IncrementCounter(() => _localDelete++);
                    Logger.WriteLine($"Similar file found, deleted from file, quality is lower, {mediaHandler.SimilarFileResult.SimilarFiles.Count}, {artist}/{mediaHandler.Album}, {mediaHandler.FileInfo.FullName}");
                }
                else
                {
                    Logger.WriteLine($"Similar file found, quality is lower, {mediaHandler.SimilarFileResult.SimilarFiles.Count}, {artist}/{mediaHandler.Album}, {mediaHandler.FileInfo.FullName}");
                }
            }
            else if (isFromPreferredQuality && isNonPreferredQuality || //overwrite lower quality based on extension
                     (isFromPreferredQuality && isSimilarPreferredQuality && mediaHandler.FileInfo.Length > similarFile.File.Length) || //overwrite based on filesize, both high quality
                     (isFromNonPreferredQuality && isNonPreferredQuality && mediaHandler.FileInfo.Length > similarFile.File.Length)) //overwrite based on filesize, both low quality
            {
                if (!toAlbumDirInfo.Exists)
                {
                    toAlbumDirInfo.Create();
                    IncrementCounter(() => _createdSubDirectories++);
                    Logger.WriteLine($"Created directory, {toAlbumDirInfo.FullName}", true);
                }

                await UpdateArtistTagAsync(updatedArtistName, mediaHandler, artist);

                await mediaHandler.GenerateSaveFingerprintAsync();
                
                bool saveSuccess = false;
                if ((saveSuccess = await _mediaTagWriteService.SafeSaveAsync(mediaHandler, new FileInfo(newFromFilePath))) &&
                    !string.Equals(mediaHandler.FileInfo.FullName, newFromFilePath))
                {
                    DumpCoverArt(mediaHandler, toAlbumDirInfo);
                    mediaHandler.FileInfo.Delete();
                }

                if (saveSuccess)
                {
                    DumpCoverArt(mediaHandler, toAlbumDirInfo);
                    Logger.WriteLine($"Moved {mediaHandler.FileInfo.Name} >> {newFromFilePath}", true);
                    
                    if (similarFile.File.FullName != newFromFilePath && _options.DeleteDuplicateTo)
                    {
                        similarFile.File.Delete();
                        IncrementCounter(() => _remoteDelete++);
                        Logger.WriteLine($"Deleting duplicated file '{similarFile.File.FullName}'");
                    }
                    IncrementCounter(() => _movedFiles++);
                    Logger.WriteLine($"Similar file found, overwriting target, From is bigger, {mediaHandler.SimilarFileResult.SimilarFiles.Count}, {artist}/{mediaHandler.Album}, {mediaHandler.FileInfo.FullName}", true);
                }
                
                RemoveCacheByPath(newFromFilePath);
                RemoveCacheByPath(similarFile.File.FullName);
            }
            else if (similarFile.File.Length == mediaHandler.FileInfo.Length)
            {
                if (_options.DeleteDuplicateFrom)
                {
                    mediaHandler.FileInfo.Delete();
                    IncrementCounter(() => _localDelete++);
                    Logger.WriteLine($"Similar file found, deleted from file, exact same size from/target, {mediaHandler.SimilarFileResult.SimilarFiles.Count}, {artist}/{mediaHandler.Album}, {mediaHandler.FileInfo.FullName}", true);
                }
            }
            else if (similarFile.File.Length > mediaHandler.FileInfo.Length)
            {
                if (_options.DeleteDuplicateFrom)
                {
                    mediaHandler.FileInfo.Delete();
                    IncrementCounter(() => _localDelete++);
                    Logger.WriteLine($"Similar file found, deleted from file, Target is bigger, {mediaHandler.SimilarFileResult.SimilarFiles.Count}, {artist}/{mediaHandler.Album}, {mediaHandler.FileInfo.FullName}", true);
                }
            }
            else
            {
                Logger.WriteLine($"[To Be Implemented] Similar file found {mediaHandler.SimilarFileResult.SimilarFiles.Count}, {artist}/{mediaHandler.Album}, {similarFile.File.Extension}");
            }
        }
        else if (mediaHandler.SimilarFileResult.SimilarFiles.Count >= 2)
        {
            bool isFromPreferredQuality = _options.PreferredFileExtensions.Any(ext => mediaHandler.FileInfo.Extension.Contains(ext));
            bool isNonPreferredQuality = _options.NonPreferredFileExtensions.Any(ext => mediaHandler.SimilarFileResult.SimilarFiles.Any(similarFile => similarFile.File.Extension.Contains(ext)));
            
            bool inputIsOutput = mediaHandler.SimilarFileResult.SimilarFiles
                .Any(sim => string.Equals(sim.File.FullName, mediaHandler.FileInfo.FullName));
            
            if (isFromPreferredQuality && isNonPreferredQuality)
            {
                if (!toAlbumDirInfo.Exists)
                {
                    toAlbumDirInfo.Create();
                    IncrementCounter(() => _createdSubDirectories++);
                    Logger.WriteLine($"Created directory, {toAlbumDirInfo.FullName}", true);
                }

                await UpdateArtistTagAsync(updatedArtistName, mediaHandler, artist);

                await mediaHandler.GenerateSaveFingerprintAsync();

                bool saveSuccess = false;
                if ((saveSuccess = await _mediaTagWriteService.SafeSaveAsync(mediaHandler, new FileInfo(newFromFilePath))) &&
                    !string.Equals(mediaHandler.FileInfo.FullName, newFromFilePath))
                {
                    DumpCoverArt(mediaHandler, toAlbumDirInfo);
                    mediaHandler.FileInfo.Delete();
                }

                if (saveSuccess)
                {
                    DumpCoverArt(mediaHandler, toAlbumDirInfo);
                    
                    Logger.WriteLine($"Moved {mediaHandler.FileInfo.Name} >> {newFromFilePath}");
                    RemoveCacheByPath(newFromFilePath);

                    if (_options.DeleteDuplicateTo)
                    {
                        mediaHandler.SimilarFileResult.SimilarFiles.ForEach(similarFile =>
                        {
                            inputIsOutput = string.Equals(similarFile.File.FullName, mediaHandler.FileInfo.FullName) ||
                                            string.Equals(similarFile.File.FullName, newFromFilePath);
                            if (similarFile.File.FullName != newFromFilePath && !inputIsOutput)
                            {
                                Logger.WriteLine($"Deleting duplicated file '{similarFile.File.FullName}'");
                                RemoveCacheByPath(similarFile.File.FullName);
                                similarFile.File.Delete();
                                IncrementCounter(() => _remoteDelete++);
                            }
                        });
                    }

                    IncrementCounter(() => _movedFiles++);
                    Logger.WriteLine($"Similar files found, overwriting target, From is bigger, {mediaHandler.SimilarFileResult.SimilarFiles.Count}, {artist}/{mediaHandler.Album}, {mediaHandler.FileInfo.FullName}");
                }
            }
            else if(_options.DeleteDuplicateFrom && !inputIsOutput)
            {
                mediaHandler.FileInfo.Delete();
                IncrementCounter(() => _localDelete++);
                Logger.WriteLine($"Similar files found, deleted from file, Targets are bigger, {mediaHandler.SimilarFileResult.SimilarFiles.Count}, {artist}/{mediaHandler.Album}, {mediaHandler.FileInfo.FullName}");
            }

            Logger.WriteLine($"Similar files found {mediaHandler.SimilarFileResult.SimilarFiles.Count}, {artist}/{mediaHandler.Album}");
        }

        return true;
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

    private async Task<SimilarFileResult> GetSimilarFileFromTagsArtistAsync(
        MediaHandler mediaHandler, 
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
                
                MediaHandler? cachedMediaHandler = null;

                lock (_memoryCache)
                {
                    cachedMediaHandler = _memoryCache.Get(toFile.FullName) as MediaHandler;
                }

                if (cachedMediaHandler == null)
                {
                    cachedMediaHandler = await AddFileToCacheAsync(toFile);
                }
                else
                {
                    IncrementCounter(() => _cachedReadTargetFiles++);
                }
                
                if (cachedMediaHandler == null)
                {
                    similarFileResult.Errors = true;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(cachedMediaHandler.Title) || 
                    string.IsNullOrWhiteSpace(cachedMediaHandler.Album) || 
                    string.IsNullOrWhiteSpace(cachedMediaHandler.Artist) || 
                    string.IsNullOrWhiteSpace(cachedMediaHandler.AlbumArtist))
                {
                    Logger.WriteLine($"scan error file, no Title, Album, Artist or AlbumArtist: {toFile}");
                    similarFileResult.Errors = true;
                    continue;
                }

                bool artistMatch = Fuzz.Ratio(cachedMediaHandler?.Artist,  mediaHandler.Artist) >= NamingAccuracy ||
                                   Fuzz.Ratio(ArtistHelper.GetUncoupledArtistName(cachedMediaHandler?.Artist), ArtistHelper.GetUncoupledArtistName(mediaHandler.Artist)) >= NamingAccuracy;
                
                bool albumArtistMatch = Fuzz.Ratio(cachedMediaHandler?.AlbumArtist, mediaHandler.AlbumArtist) >= NamingAccuracy ||
                                        Fuzz.Ratio(ArtistHelper.GetUncoupledArtistName(cachedMediaHandler?.AlbumArtist), ArtistHelper.GetUncoupledArtistName(mediaHandler.AlbumArtist)) >= NamingAccuracy;
                
                if (Fuzz.Ratio(cachedMediaHandler?.Title, mediaHandler.Title) >= NamingAccuracy &&
                    Fuzz.Ratio(cachedMediaHandler?.Album,  mediaHandler.Album) >= NamingAccuracy &&
                    cachedMediaHandler?.TrackNumber == mediaHandler.TrackNumber &&
                    artistMatch && albumArtistMatch)
                {
                    similarFileResult.SimilarFiles.Add(new SimilarFileInfo(toFile, cachedMediaHandler));
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

    private async Task<MediaHandler?> AddFileToCacheAsync(FileInfo fileInfo)
    {
        IncrementCounter(() => _scannedTargetFiles++);
        MediaHandler? cachedMediaInfo = await GetMediaFileHandlerAsync(fileInfo);

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
    
    private async Task UpdateArtistTagAsync(bool updatedArtistName, MediaHandler mediaHandler, string artist)
    {
        if (updatedArtistName && _options.UpdateArtistTags)
        {
            await _mediaTagWriteService.UpdateArtistAsync(mediaHandler, artist);
            IncrementCounter(() => _updatedTagfiles++);
        }
    }

    private string SanitizeAlbumName(string albumName)
    {
        albumName = ArtistHelper.GetShortWordVersion(albumName, MaxFilePartNameLength);
        return albumName
            .Replace('/', '+')
            .Replace('\\', '+');
    }

    private string SanitizeArtistName(string artistName)
    {
        artistName = ArtistHelper.GetShortWordVersion(artistName, MaxFilePartNameLength);
        return artistName
            .Replace('/', '+')
            .Replace('\\', '+');
    }

    private string SanitizeFileName(string fileName)
    {
        fileName = ArtistHelper.GetShortWordVersion(fileName, MaxFilePartNameLength - 5);
        return fileName
            .Replace('/', '+')
            .Replace('\\', '+');
    }

    private void ShowProgress()
    {
        Logger.WriteLine($"Stats: Moved {_movedFiles}, " +
                           (_options.DeleteDuplicateFrom ? $"Local Delete: {_localDelete}, " : string.Empty) +
                           (_options.DeleteDuplicateTo ? $"Remote Delete: {_remoteDelete}, " : string.Empty) +
                           (_options.FixFileCorruption ? $"Fixed Corrupted: {_fixedCorruptedFiles}, " : string.Empty) +
                           (_options.UpdateArtistTags ? $"Updated Artist Tags: {_updatedTagfiles}, " : string.Empty) +
                           (_options.CreateAlbumDirectory ? $"Created SubDirectories: {_createdSubDirectories}, " : string.Empty) +
                           $"Scanned From: {_scannedFromFiles}. " +
                           (!_options.OnlyFileNameMatching ? $"Cached Read Target: {_cachedReadTargetFiles}. " : string.Empty) +
                           (!_options.OnlyFileNameMatching ? $"Scanned Target: {_scannedTargetFiles}. " : string.Empty) +
                           $"Skipped Error: {_skippedErrorFiles}, " +
                           $"Running: {_runtimeSw.Elapsed.Hours:D2}:{_runtimeSw.Elapsed.Minutes:D2}:{_runtimeSw.Elapsed.Seconds:D2}");
    }

    private void DumpCoverArt(MediaHandler mediaHandler, DirectoryInfo toAlbumDirInfo)
    {
        if (!string.IsNullOrWhiteSpace(_options.DumpCoverFilename))
        {
            mediaHandler.DumpCover(new FileInfo(Path.Join(toAlbumDirInfo.FullName, _options.DumpCoverFilename)));
        }
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
    
    public string GetFormatName(MediaHandler mediaHandler, string format,  string seperator)
    {
        mediaHandler.SetMediaTagValue(ReplaceDirectorySeparators(mediaHandler.Artist, seperator), "Artist");
        mediaHandler.SetMediaTagValue(ReplaceDirectorySeparators(mediaHandler.Title, seperator), "Title");
        mediaHandler.SetMediaTagValue(ReplaceDirectorySeparators(mediaHandler.Album, seperator), "Album");
        
        format = Smart.Format(format, mediaHandler);
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

    private async Task TagFileAcoustIdAsync(MediaHandler mediaHandler)
    {
        if (!string.IsNullOrWhiteSpace(_options.AcoustIdApiKey) &&
            (_options.AlwaysCheckAcoustId ||
             string.IsNullOrWhiteSpace(mediaHandler.Artist) ||
             string.IsNullOrWhiteSpace(mediaHandler.Album) ||
             string.IsNullOrWhiteSpace(mediaHandler.AlbumArtist)))
        {
            var match = await _musicBrainzService.GetMatchFromAcoustIdAsync(mediaHandler,
                _options.AcoustIdApiKey,
                _options.SearchByTagNames,
                _options.AcoustIdMatchPercentage,
                _options.MusicBrainzMatchPercentage,
                _options.AcoustIdMaxTimeSpan);
            
            bool success =
                match != null && await _musicBrainzService.WriteTagFromAcoustIdAsync(
                    match,
                    mediaHandler, 
                    _options.OverwriteArtist, 
                    _options.OverwriteAlbum, 
                    _options.OverwriteTrack,
                    _options.OverwriteAlbumArtist);
        
            mediaHandler.MusicBrainzTaggingSuccess = success;
            
            if (success)
            {
                Logger.WriteLine($"Updated with AcoustId/MusicBrainz tags {mediaHandler.FileInfo.FullName}", true);
            }
            else
            {
                Logger.WriteLine($"AcoustId not found by Fingerprint for {mediaHandler.FileInfo.FullName}", true);
            }
        }
    }

    private async Task TagFileMetadataApiAsync(MediaHandler mediaHandler)
    {
        if (!string.IsNullOrWhiteSpace(_options.MetadataApiBaseUrl))
        {
            var matches = await _miniMediaMetadataService.GetMatchesAsync(
                mediaHandler,
                _options.MetadataApiMatchPercentage);

            bool success = false;
            foreach (var match in matches)
            {
                if (await _miniMediaMetadataService.WriteTagsToFileAsync(
                        match,
                        mediaHandler,
                        _options.OverwriteArtist,
                        _options.OverwriteAlbum,
                        _options.OverwriteTrack,
                        _options.OverwriteAlbumArtist))
                {
                    success = true;
                }
            }
            
            mediaHandler.MetadataApiTaggingSuccess = success;
            if (success)
            {
                Logger.WriteLine($"Updated with MetadataAPI tags {mediaHandler.FileInfo.FullName}", true);
            }
            else
            {
                Logger.WriteLine($"MetadataAPI track not found for {mediaHandler.FileInfo.FullName}", true);
            }
        }
    }

    private async Task TagFileTidalAsync(MediaHandler mediaHandler)
    {
        if (!mediaHandler.MetadataApiTaggingSuccess &&
            !string.IsNullOrWhiteSpace(_options.TidalClientId) &&
            !string.IsNullOrWhiteSpace(_options.TidalClientSecret) &&
            !string.IsNullOrWhiteSpace(_options.TidalCountryCode))
        {
            bool success = await _tidalService.WriteTagsAsync(mediaHandler, 
                _options.OverwriteArtist, 
                _options.OverwriteAlbum, 
                _options.OverwriteTrack,
                _options.OverwriteAlbumArtist,
                _options.TidalMatchPercentage);
            
            mediaHandler.TidalTaggingSuccess = success;
            if (success)
            {
                Logger.WriteLine($"Updated with Tidal tags {mediaHandler.FileInfo.FullName}", true);
            }
            else
            {
                Logger.WriteLine($"Tidal record not found by MediaTag information for {mediaHandler.FileInfo.FullName}", true);
            }
        }
    }
}