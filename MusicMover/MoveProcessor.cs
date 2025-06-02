using System.Diagnostics;
using System.Runtime.Caching;
using FuzzySharp;
using ListRandomizer;
using MusicMover.Models;
using MusicMover.Services;
using SmartFormat;

namespace MusicMover;

public class MoveProcessor
{
    private readonly string[] _mediaFileExtensions = new string[]
    {
        "flac",
        "mp3",
        "m4a",
        "wav",
        "aaif",
        "opus"
    };

    private readonly string[] _lowerQualityMediaExtensions = new string[]
    {
        "mp3",
        "wav",
        "aaif",
        "opus"
    };
    private readonly string[] _higherQualityMediaExtensions = new string[]
    {
        "flac",
        "m4a"
    };

    private const long MinAvailableDiskSpace = 5000; //GB
    private const int CacheTime = 5; //minutes
    private const string VariousArtistsName = "Various Artists";
    private const string AcoustidFingerprintTag = "Acoustid Fingerprint";
    private const string AcoustidTag = "Acoustid Id";

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
    
    private List<string> _artistsNotFound = new List<string>();

    public MoveProcessor(CliOptions options)
    {
        _options = options;
        _memoryCache = MemoryCache.Default;
        _corruptionFixer = new CorruptionFixer();
        _musicBrainzService = new MusicBrainzService();
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
            Console.WriteLine($"[{directoriesRead++}/{topDirectories.Count}] Reading files from '{directoryPath}'");
            filesToProcess.AddRange(await GetMediaFileListAsync(directoryPath, SearchOption.AllDirectories));
        }

        filesToProcess = filesToProcess
            .OrderBy(file => file.Artist)
            .ThenBy(file => file.AlbumArtist)
            .ThenBy(file => file.Album)
            .ToList();

        //process directories
        if (_options.Parallel)
        {
            foreach (var mediaFileInfo in filesToProcess
                         .AsParallel()
                         .WithDegreeOfParallelism(5))
            {
                try
                {
                    await ProcessFromFileAsync(mediaFileInfo);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{mediaFileInfo.FileInfo.FullName}, {e.Message}. \r\n{e.StackTrace}");
                    IncrementCounter(() => _skippedErrorFiles++);
                }
            }
        }
        else
        {
            foreach (var mediaFileInfo in filesToProcess)
            {
                try
                {
                    await ProcessFromFileAsync(mediaFileInfo);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{mediaFileInfo.FileInfo.FullName}, {e.Message}. \r\n{e.StackTrace}");
                    IncrementCounter(() => _skippedErrorFiles++);
                }
            }
        }
        
        ShowProgress();

        if (_artistsNotFound.Count > 0)
        {
            Console.WriteLine($"Artists not found: {_artistsNotFound.Count}");
            _artistsNotFound.ForEach(artist => Console.WriteLine(artist));
        }
    }

    private async Task<List<MediaFileInfo>> GetMediaFileListAsync(string fromTopDir, SearchOption dirSearchOption)
    {
        List<MediaFileInfo> filesToProcess = new List<MediaFileInfo>();
        DirectoryInfo fromDirInfo = new DirectoryInfo(fromTopDir);

        FileInfo[] fromFiles = fromDirInfo
            .GetFiles("*.*", dirSearchOption)
            .Where(file => file.Length > 0)
            .Where(file => _mediaFileExtensions.Any(ext => file.Name.ToLower().EndsWith(ext)))
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
                Console.WriteLine($"{ex.Message}, {fromFile.FullName}");
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
                Console.WriteLine($"{ex.Message}, {fromFile.FullName}");
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
            Debug.WriteLine($"Artist {artist} does not exist");
        }

        return toArtistDirInfo.Exists;
    }

    private async Task<bool> ProcessFromFileAsync(MediaFileInfo mediaFileInfo)
    {
        if (!EnoughDiskSpace())
        {
            if (!_exitProcess)
            {
                Console.WriteLine("Not enough diskspace left! <5GB on target directory>");
            }
            _exitProcess = true;
            return false;
        }

        if (!mediaFileInfo.FileInfo.Exists)
        {
            Console.WriteLine($"Media file no longer exists '{mediaFileInfo.FileInfo.FullName}'");
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

        if (!string.IsNullOrWhiteSpace(_options.AcoustIdApiKey) &&
            (_options.AlwaysCheckAcoustId ||
             string.IsNullOrWhiteSpace(mediaFileInfo.Artist) ||
             string.IsNullOrWhiteSpace(mediaFileInfo.Album) ||
             string.IsNullOrWhiteSpace(mediaFileInfo.AlbumArtist)))
        {
            musicBrainzTaggingSuccess = await _musicBrainzService.WriteTagFromAcoustIdAsync(mediaFileInfo, mediaFileInfo.FileInfo, _options.AcoustIdApiKey,
                _options.OverwriteArtist, _options.OverwriteAlbum, _options.OverwriteTrack,
                _options.OverwriteAlbumArtist, _options.SearchByTagNames);
            
            if (musicBrainzTaggingSuccess)
            {
                //read again the file after saving
                mediaFileInfo = new MediaFileInfo(mediaFileInfo.FileInfo);
                Console.WriteLine($"Updated with AcoustId/MusicBrainz tags {mediaFileInfo.FileInfo.FullName}");
            }
            else
            {
                Console.WriteLine($"AcoustId not found by Fingerprint for {mediaFileInfo.FileInfo.FullName}");
            }
        }

        if (!string.IsNullOrWhiteSpace(_options.MetadataApiBaseUrl))
        {
            metadataApiTaggingSuccess = await _miniMediaMetadataService.WriteTagsAsync(mediaFileInfo, 
                mediaFileInfo.FileInfo, 
                GetUncoupledArtistName(mediaFileInfo.Artist), 
                GetUncoupledArtistName(mediaFileInfo.AlbumArtist),
                _options.OverwriteArtist, _options.OverwriteAlbum, _options.OverwriteTrack,
                _options.OverwriteAlbumArtist);
            
            if (metadataApiTaggingSuccess)
            {
                //read again the file after saving
                mediaFileInfo = new MediaFileInfo(mediaFileInfo.FileInfo);
                Console.WriteLine($"Updated with Tidal tags {mediaFileInfo.FileInfo.FullName}");
            }
            else
            {
                Console.WriteLine($"Tidal track not found for {mediaFileInfo.FileInfo.FullName}");
            }
        }
        
        if (!metadataApiTaggingSuccess &&
            !string.IsNullOrWhiteSpace(_options.TidalClientId) &&
            !string.IsNullOrWhiteSpace(_options.TidalClientSecret) &&
            !string.IsNullOrWhiteSpace(_options.TidalCountryCode))
        {
            tidalTaggingSuccess = await _tidalService.WriteTagsAsync(mediaFileInfo, 
                mediaFileInfo.FileInfo, 
                GetUncoupledArtistName(mediaFileInfo.Artist), 
                GetUncoupledArtistName(mediaFileInfo.AlbumArtist),
                _options.OverwriteArtist, _options.OverwriteAlbum, _options.OverwriteTrack,
                _options.OverwriteAlbumArtist);
            
            if (tidalTaggingSuccess)
            {
                //read again the file after saving
                mediaFileInfo = new MediaFileInfo(mediaFileInfo.FileInfo);
                Console.WriteLine($"Updated with Tidal tags {mediaFileInfo.FileInfo.FullName}");
            }
            else
            {
                Console.WriteLine($"Tidal record not found by MediaTag information for {mediaFileInfo.FileInfo.FullName}");
            }
        }
        
        if (_options.OnlyMoveWhenTagged && !musicBrainzTaggingSuccess && !tidalTaggingSuccess && !metadataApiTaggingSuccess)
        {
            Console.WriteLine($"Skipped processing, tagging failed for '{mediaFileInfo.FileInfo.FullName}'");
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
            Console.WriteLine($"File is missing Artist, Album or title in the tags, skipping, {mediaFileInfo.FileInfo.FullName}");
            return false;
        }

        if (!String.IsNullOrWhiteSpace(_options.FileFormat))
        {
            string oldFileName = mediaFileInfo.FileInfo.Name;
            string newFileName = GetFormatName(mediaFileInfo, _options.FileFormat, _options.DirectorySeperator) + mediaFileInfo.FileInfo.Extension;
            string newFilePath = Path.Join(mediaFileInfo.FileInfo.Directory.FullName, newFileName);
            File.Move(mediaFileInfo.FileInfo.FullName, newFilePath, true);
            mediaFileInfo = new MediaFileInfo(new FileInfo(newFilePath));
            Console.WriteLine($"Renamed '{oldFileName}' => '{newFileName}'");
        }

        DirectoryInfo toArtistDirInfo = new DirectoryInfo($"{_options.ToDirectory}{SanitizeArtistName(artist)}");
        DirectoryInfo toAlbumDirInfo = new DirectoryInfo($"{_options.ToDirectory}{SanitizeArtistName(artist)}/{SanitizeAlbumName(mediaFileInfo.Album)}");

        string? newArtistName = GetUncoupledArtistName(artist);

        if (!string.IsNullOrWhiteSpace(newArtistName) && newArtistName != artist)
        {
            updatedArtistName = true;
            if (!SetToArtistDirectory(newArtistName, mediaFileInfo, out toArtistDirInfo, out toAlbumDirInfo))
            {
                if (!_artistsNotFound.Contains(newArtistName))
                {
                    _artistsNotFound.Add(newArtistName);
                }
                
                Console.WriteLine($"Skipped processing, Artist folder does not exist for '{mediaFileInfo.FileInfo.FullName}'");
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
                Console.WriteLine($"Skipped processing, Artist folder does not exist for '{mediaFileInfo.FileInfo.FullName}'");
                return false;
            }
        }

        if (!toArtistDirInfo.Exists)
        {
            Console.WriteLine($"Skipped processing, Artist folder does not exist for '{mediaFileInfo.FileInfo.FullName}'");
            return false;
        }

        SimilarFileResult similarFileResult = await GetSimilarFileFromTagsArtistAsync(mediaFileInfo, mediaFileInfo.FileInfo, toAlbumDirInfo, artist);

        if (similarFileResult.Errors && !_options.ContinueScanError)
        {
            Console.WriteLine($"Scan errors... skipping {mediaFileInfo.FileInfo.FullName}");
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

            var extraSimilarResult = await GetSimilarFileFromTagsArtistAsync(mediaFileInfo, mediaFileInfo.FileInfo, albumDirInfo, artist);

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
            Console.WriteLine($"Skipping file, artist '{artist}' does not exist in extra directory {mediaFileInfo.FileInfo.FullName}");
            return false;
        }

        if (similarFileResult.Errors && !_options.ContinueScanError)
        {
            Console.WriteLine($"Scan errors... skipping {mediaFileInfo.FileInfo.FullName}");
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
            Console.WriteLine($"No similar files found moving, {artist}/{mediaFileInfo.Album}, {newFromFilePath}");
            if (!toAlbumDirInfo.Exists)
            {
                toAlbumDirInfo.Create();
                IncrementCounter(() => _createdSubDirectories++);

                Console.WriteLine($"Created directory, {toAlbumDirInfo.FullName}");
            }

            UpdateArtistTag(updatedArtistName, mediaFileInfo, oldArtistName, artist, mediaFileInfo.FileInfo);
            mediaFileInfo.FileInfo.MoveTo(newFromFilePath, true);
            RemoveCacheByPath(newFromFilePath);

            IncrementCounter(() => _movedFiles++);
        }
        else if (similarFileResult.SimilarFiles.Count == 1)
        {
            var similarFile = similarFileResult.SimilarFiles.First();

            bool isFromHighQuality = _higherQualityMediaExtensions.Any(ext => mediaFileInfo.FileInfo.Extension.Contains(ext));
            bool isFromLowerQuality = _lowerQualityMediaExtensions.Any(ext => mediaFileInfo.FileInfo.Extension.Contains(ext));
            bool isSimilarHighQuality = _higherQualityMediaExtensions.Any(ext => similarFile.File.Extension.Contains(ext));
            bool isSimilarLowerQuality = _lowerQualityMediaExtensions.Any(ext => similarFile.File.Extension.Contains(ext));

            if (!isFromHighQuality && isSimilarHighQuality)
            {
                if (_options.DeleteDuplicateFrom)
                {
                    mediaFileInfo.FileInfo.Delete();
                    IncrementCounter(() => _localDelete++);
                    Console.WriteLine($"Similar file found, deleted from file, quality is lower, {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}, {mediaFileInfo.FileInfo.FullName}");
                }
                else
                {
                    Console.WriteLine($"Similar file found, quality is lower, {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}, {mediaFileInfo.FileInfo.FullName}");
                }
            }
            else if (isFromHighQuality && isSimilarLowerQuality || //overwrite lower quality based on extension
                     (isFromHighQuality && isSimilarHighQuality && mediaFileInfo.FileInfo.Length > similarFile.File.Length) || //overwrite based on filesize, both high quality
                     (isFromLowerQuality && isSimilarLowerQuality && mediaFileInfo.FileInfo.Length > similarFile.File.Length)) //overwrite based on filesize, both low quality
            {
                if (!toAlbumDirInfo.Exists)
                {
                    toAlbumDirInfo.Create();
                    IncrementCounter(() => _createdSubDirectories++);
                    Debug.WriteLine($"Created directory, {toAlbumDirInfo.FullName}");
                }

                UpdateArtistTag(updatedArtistName, mediaFileInfo, oldArtistName, artist, mediaFileInfo.FileInfo);
                Console.WriteLine($"Moved {mediaFileInfo.FileInfo} >> {newFromFilePath}");
                mediaFileInfo.FileInfo.MoveTo(newFromFilePath, true);
                
                
                RemoveCacheByPath(newFromFilePath);
                RemoveCacheByPath(similarFile.File.FullName);

                if (similarFile.File.FullName != newFromFilePath && _options.DeleteDuplicateTo)
                {
                    similarFile.File.Delete();
                    IncrementCounter(() => _remoteDelete++);
                    Console.WriteLine($"Deleting duplicated file '{similarFile.File.FullName}'");
                }

                IncrementCounter(() => _movedFiles++);

                Console.WriteLine($"Similar file found, overwriting target, From is bigger, {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}, {mediaFileInfo.FileInfo.FullName}");
            }
            else if (similarFile.File.Length == mediaFileInfo.FileInfo.Length)
            {
                if (_options.DeleteDuplicateFrom)
                {
                    mediaFileInfo.FileInfo.Delete();
                    IncrementCounter(() => _localDelete++);
                    Console.WriteLine($"Similar file found, deleted from file, exact same size from/target, {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}, {mediaFileInfo.FileInfo.FullName}");
                }
            }
            else if (similarFile.File.Length > mediaFileInfo.FileInfo.Length)
            {
                if (_options.DeleteDuplicateFrom)
                {
                    mediaFileInfo.FileInfo.Delete();
                    IncrementCounter(() => _localDelete++);
                    Console.WriteLine($"Similar file found, deleted from file, Target is bigger, {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}, {mediaFileInfo.FileInfo.FullName}");
                }
            }
            else
            {
                Console.WriteLine($"[To Be Implemented] Similar file found {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}, {similarFile.File.Extension}");
            }
        }
        else if (similarFileResult.SimilarFiles.Count >= 2)
        {
            bool isFromHighQuality = _higherQualityMediaExtensions.Any(ext => mediaFileInfo.FileInfo.Extension.Contains(ext));
            bool isSimilarLowerQuality = _lowerQualityMediaExtensions.Any(ext => similarFileResult.SimilarFiles.Any(similarFile => similarFile.File.Extension.Contains(ext)));
            
            if (isFromHighQuality && isSimilarLowerQuality)
            {
                if (!toAlbumDirInfo.Exists)
                {
                    toAlbumDirInfo.Create();
                    IncrementCounter(() => _createdSubDirectories++);
                    Debug.WriteLine($"Created directory, {toAlbumDirInfo.FullName}");
                }

                UpdateArtistTag(updatedArtistName, mediaFileInfo, oldArtistName, artist, mediaFileInfo.FileInfo);
                Console.WriteLine($"Moved {mediaFileInfo.FileInfo} >> {newFromFilePath}");
                mediaFileInfo.FileInfo.MoveTo(newFromFilePath, true);
                RemoveCacheByPath(newFromFilePath);

                if (_options.DeleteDuplicateTo)
                {
                    similarFileResult.SimilarFiles.ForEach(similarFile =>
                    {
                        if (similarFile.File.FullName != newFromFilePath)
                        {
                            Console.WriteLine($"Deleting duplicated file '{similarFile.File.FullName}'");
                            RemoveCacheByPath(similarFile.File.FullName);
                            similarFile.File.Delete();
                            IncrementCounter(() => _remoteDelete++);
                        }
                    });
                }

                IncrementCounter(() => _movedFiles++);
                Console.WriteLine($"Similar files found, overwriting target, From is bigger, {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}, {mediaFileInfo.FileInfo.FullName}");
            }
            else if(_options.DeleteDuplicateFrom)
            {
                mediaFileInfo.FileInfo.Delete();
                IncrementCounter(() => _localDelete++);
                Console.WriteLine($"Similar files found, deleted from file, Targets are bigger, {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}, {mediaFileInfo.FileInfo.FullName}");
            }

            Console.WriteLine($"Similar files found {similarFileResult.SimilarFiles.Count}, {artist}/{mediaFileInfo.Album}");
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
        DirectoryInfo toAlbumDirInfo, 
        string artistName)
    {
        SimilarFileResult similarFileResult = new SimilarFileResult();

        if (!toAlbumDirInfo.Exists)
        {
            return similarFileResult;
        }
        
        List<FileInfo> toFiles = toAlbumDirInfo
            .GetFiles("*.*", SearchOption.AllDirectories)
            .Where(file => file.Length > 0)
            .Where(file => _mediaFileExtensions.Any(ext => file.Name.ToLower().EndsWith(ext)))
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
                    Console.WriteLine($"scan error file, no Title, Album, Artist or AlbumArtist: {toFile}");
                    similarFileResult.Errors = true;
                    continue;
                }

                bool artistMatch = Fuzz.Ratio(cachedMediaInfo?.Artist,  matchTagFile.Artist) >= NamingAccuracy ||
                                   Fuzz.Ratio(GetUncoupledArtistName(cachedMediaInfo?.Artist), GetUncoupledArtistName(matchTagFile.Artist)) >= NamingAccuracy;
                
                bool albumArtistMatch = Fuzz.Ratio(cachedMediaInfo?.AlbumArtist, matchTagFile.AlbumArtist) >= NamingAccuracy ||
                                        Fuzz.Ratio(GetUncoupledArtistName(cachedMediaInfo?.AlbumArtist), GetUncoupledArtistName(matchTagFile.AlbumArtist)) >= NamingAccuracy;
                
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
                Console.WriteLine($"scan error file, {e.Message}, {toFile.FullName}");
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
            Console.WriteLine($"File gave read error, '{fileInfo.FullName}', {e.Message}");
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
            Console.WriteLine($"File gave read error, '{fileInfo.FullName}', {e.Message}");
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
        Console.WriteLine($"Stats: Moved {_movedFiles}, " +
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

    private string GetUncoupledArtistName(string? artist)
    {
        if (string.IsNullOrWhiteSpace(artist))
        {
            return string.Empty;
        }
        
        var splitCharacters = new string[]
        {
            ",",
            "&",
            "+",
            "/",
            " feat",
            ";"
        };

        string? newArtistName = splitCharacters
            .Where(splitChar => artist.Contains(splitChar))
            .Select(splitChar => artist.Substring(0, artist.IndexOf(splitChar)).Trim())
            .Where(split => split.Length > 0)
            .OrderBy(split => split.Length)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(newArtistName))
        {
            return artist;
        }
        return newArtistName;
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