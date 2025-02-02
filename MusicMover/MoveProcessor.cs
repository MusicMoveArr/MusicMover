using System.Diagnostics;
using System.Runtime.Caching;
using MusicMover.Models;
using MusicMover.Services;
using SmartFormat;

namespace MusicMover;

public class MoveProcessor
{
    private string[] MediaFileExtensions = new string[]
    {
        "flac",
        "mp3",
        "m4a",
        "wav",
        "aaif",
        "opus"
    };

    private string[] LowerQualityMediaExtensions = new string[]
    {
        "mp3",
        "wav",
        "aaif",
        "opus"
    };
    private string[] HigherQualityMediaExtensions = new string[]
    {
        "flac",
        "m4a"
    };

    private const long MinAvailableDiskSpace = 5000; //GB
    private const int CacheTime = 5; //minutes
    private const string VariousArtistsName = "Various Artists";
    private const string AcoustidFingerprintTag = "Acoustid Fingerprint";
    private const string AcoustidTag = "Acoustid Id";
    
    private int movedFiles = 0;
    private int localDelete = 0;
    private int remoteDelete = 0;
    private int createdSubDirectories = 0;
    private int scannedFromFiles = 0;
    private int skippedErrorFiles = 0;
    private int scannedTargetFiles = 0;
    private int cachedReadTargetFiles = 0;
    private int fixedCorruptedFiles = 0;
    private object CounterLock = new object();
    private int updatedTagfiles = 0;
    private bool exitProcess = false;

    private Stopwatch sw = Stopwatch.StartNew();
    private Stopwatch runtimeSw = Stopwatch.StartNew();

    private CliOptions _options;
    private MemoryCache _memoryCache;
    private CorruptionFixer _corruptionFixer;
    
    private List<string> ArtistsNotFound = new List<string>();

    public MoveProcessor(CliOptions options)
    {
        _options = options;
        _memoryCache = MemoryCache.Default;
        _corruptionFixer = new CorruptionFixer();
    }

    public void Process()
    {
        var sortedTopDirectories = Directory
            .EnumerateFileSystemEntries(_options.FromDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(file => Directory.Exists(file))
            .Where(dir => !dir.Contains(".Trash"))
            .OrderBy(dir => dir)
            .Skip(_options.SkipFromDirAmount)
            .ToList();

        if (_options.Parallel)
        {
            sortedTopDirectories
                .AsParallel()
                .WithDegreeOfParallelism(5)
                .ForAll(dir => ProcessDirectory(dir));
        }
        else
        {
            sortedTopDirectories
                .ForEach(dir => ProcessDirectory(dir));
        }

        ShowProgress();

        if (ArtistsNotFound.Count > 0)
        {
            Console.WriteLine($"Artists not found: {ArtistsNotFound.Count}");
            ArtistsNotFound.ForEach(artist => Console.WriteLine(artist));
        }
    }

    private void ProcessDirectory(string fromTopDir)
    {
        if (exitProcess)
        {
            return;
        }
        
        DirectoryInfo fromDirInfo = new DirectoryInfo(fromTopDir);

        FileInfo[] fromFiles = fromDirInfo
            .GetFiles("*.*", SearchOption.AllDirectories)
            .Where(file => file.Length > 0)
            .Where(file => MediaFileExtensions.Any(ext => file.Name.ToLower().EndsWith(ext)))
            .ToArray();

        foreach (FileInfo fromFile in fromFiles)
        {
            if (!EnoughDiskSpace())
            {
                if (!exitProcess)
                {
                    Console.WriteLine("Not enough diskspace left! <5GB on target directory>");
                }
                exitProcess = true;
                break;
            }
            
            lock (sw)
            {
                if (sw.Elapsed.Seconds >= 5)
                {
                    ShowProgress();
                    sw.Restart();
                }
            }

            IncrementCounter(() => scannedFromFiles++);

            Debug.WriteLine($"File: {fromFile.FullName}");
            try
            {
                ProcessFromFile(fromFile);
            }
            catch (Exception e)
            {
                Console.WriteLine($"{fromFile.FullName}, {e.Message}");
                IncrementCounter(() => skippedErrorFiles++);
            }
        }
    }

    private bool SetToArtistDirectory(string artist, MediaFileInfo tagFile,
        out DirectoryInfo toArtistDirInfo,
        out DirectoryInfo toAlbumDirInfo)
    {
        toArtistDirInfo = new DirectoryInfo($"{_options.ToDirectory}{SanitizeArtistName(artist)}");
        toAlbumDirInfo = new DirectoryInfo($"{_options.ToDirectory}{SanitizeArtistName(artist)}/{SanitizeAlbumName(tagFile.Album)}");

        if (!toArtistDirInfo.Exists &&
            _options.CreateArtistDirectory &&
            !_options.IsDryRun)
        {
            bool aristExists = _options.ArtistDirsMustNotExist.Any(dir =>
            {
                var extraToArtistDirInfo = new DirectoryInfo($"{dir}{SanitizeArtistName(artist)}");
                return extraToArtistDirInfo.Exists;
            });
            
            if (!aristExists)
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

    private bool ProcessFromFile(FileInfo fromFile)
    {
        MediaFileInfo tagFile = null;

        try
        {
            tagFile = new MediaFileInfo(fromFile);
        }
        catch (Exception e)
        {
            IncrementCounter(() => skippedErrorFiles++);

            Console.WriteLine($"{e.Message}, {fromFile.FullName}");
        }

        try
        {
            if (tagFile is null &&
                _options.FixFileCorruption &&
                _corruptionFixer.FixCorruption(fromFile))
            {
                tagFile = new MediaFileInfo(fromFile);
                IncrementCounter(() => fixedCorruptedFiles++);
            }
        }
        catch (Exception exception)
        {
            return false;
        }

        if (tagFile is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_options.AcoustIdAPIKey) &&
            (_options.AlwaysCheckAcoustId ||
             string.IsNullOrWhiteSpace(tagFile.Artist) ||
             string.IsNullOrWhiteSpace(tagFile.Album) ||
             string.IsNullOrWhiteSpace(tagFile.AlbumArtist)))
        {
            MediaTagWriteService mediaTagWriteService = new MediaTagWriteService();
            if (mediaTagWriteService.WriteTagFromAcoustId(fromFile, _options.AcoustIdAPIKey))
            {
                //read again the file after saving
                tagFile = new MediaFileInfo(fromFile);
                Console.WriteLine($"Updated with AcoustId/MusicBrainz tags {fromFile.FullName}");
            }
            else
            {
                Console.WriteLine($"AcoustId not found by Fingerprint for {fromFile.FullName}");
            }
        }
        
        string oldArtistName = tagFile.AlbumArtist;
        string artist = tagFile.AlbumArtist;
        bool updatedArtistName = false;

        if (string.IsNullOrWhiteSpace(artist) ||
            (artist.Contains(VariousArtistsName) && !string.IsNullOrWhiteSpace(tagFile.Artist)))
        {
            artist = tagFile.Artist;
        }
        
        if (string.IsNullOrWhiteSpace(artist) ||
            (artist.Contains(VariousArtistsName) && !string.IsNullOrWhiteSpace(tagFile.SortArtist)))
        {
            artist = tagFile.SortArtist;
        }

        if (string.IsNullOrWhiteSpace(artist) ||
            string.IsNullOrWhiteSpace(tagFile.Album) ||
            string.IsNullOrWhiteSpace(tagFile.Title))
        {
            Console.WriteLine($"File is missing Artist, Album or title in the tags, skipping, {fromFile.FullName}");
            return false;
        }

        if (!String.IsNullOrWhiteSpace(_options.FileFormat))
        {
            string oldFileName = tagFile.FileInfo.Name;
            string newFileName = GetFormatName(tagFile, _options.FileFormat, _options.DirectorySeperator) + tagFile.FileInfo.Extension;
            string newFilePath = Path.Join(tagFile.FileInfo.Directory.FullName, newFileName);
            File.Move(tagFile.FileInfo.FullName, newFilePath, true);
            fromFile = new FileInfo(newFilePath);
            tagFile = new MediaFileInfo(fromFile);
            Console.WriteLine($"Renamed '{oldFileName}' => '{newFileName}'");
        }

        DirectoryInfo toArtistDirInfo = new DirectoryInfo($"{_options.ToDirectory}{SanitizeArtistName(artist)}");
        DirectoryInfo toAlbumDirInfo = new DirectoryInfo($"{_options.ToDirectory}{SanitizeArtistName(artist)}/{SanitizeAlbumName(tagFile.Album)}");

        string? newArtistName = GetUncoupledArtistName(artist);

        if (!string.IsNullOrWhiteSpace(newArtistName) && newArtistName != artist)
        {
            updatedArtistName = true;
            if (!SetToArtistDirectory(newArtistName, tagFile, out toArtistDirInfo, out toAlbumDirInfo))
            {
                if (!ArtistsNotFound.Contains(newArtistName))
                {
                    ArtistsNotFound.Add(newArtistName);
                }
                return false;
            }

            artist = newArtistName;
        }
        else
        {
            if (!SetToArtistDirectory(artist, tagFile, out toArtistDirInfo, out toAlbumDirInfo))
            {
                if (!ArtistsNotFound.Contains(artist))
                {
                    ArtistsNotFound.Add(artist);
                }
                return false;
            }
        }

        if (!toArtistDirInfo.Exists)
        {
            return false;
        }

        bool scanErrors = false;

        List<SimilarFileInfo> similarFiles = GetSimilarFileFromTagsArtist(tagFile, fromFile, toArtistDirInfo, artist, out scanErrors);

        if (scanErrors && !_options.ContinueScanError)
        {
            Console.WriteLine($"Scan errors... skipping {fromFile.FullName}");
            return false;
        }

        bool extraDirExists = true;
        foreach (string extraScaDir in _options.ExtraScans)
        {
            DirectoryInfo extraDirInfo = new DirectoryInfo($"{extraScaDir}{SanitizeArtistName(artist)}");

            if (!extraDirInfo.Exists)
            {
                extraDirExists = false;
                continue;
            }

            var extraSimilarFiles = GetSimilarFileFromTagsArtist(tagFile, fromFile, extraDirInfo, artist, out scanErrors);

            if (scanErrors && !_options.ContinueScanError)
            {
                break;
            }

            if (extraSimilarFiles.Count > 0)
            {
                similarFiles.AddRange(extraSimilarFiles);
            }
        }

        if (!extraDirExists && _options.ExtraDirMustExist)
        {
            Console.WriteLine($"Skipping file, artist '{artist}' does not exist in extra directory {fromFile.FullName}");
            return false;
        }

        if (scanErrors && !_options.ContinueScanError)
        {
            Console.WriteLine($"Scan errors... skipping {fromFile.FullName}");
            IncrementCounter(() => skippedErrorFiles++);
            return false;
        }

        string fromFileName = fromFile.Name;

        if (_options.RenameVariousArtists &&
            fromFileName.Contains(VariousArtistsName))
        {
            fromFileName = fromFileName.Replace(VariousArtistsName, artist);
        }

        string newFromFilePath = $"{toAlbumDirInfo.FullName}/{fromFileName}";
        
        
        if (toArtistDirInfo.Name.Contains(",") ||
            toArtistDirInfo.Name.Contains(";"))
        {
                
        }

        if (similarFiles.Count == 0)
        {
            Console.WriteLine($"No similar files found moving, {artist}/{tagFile.Album}, {newFromFilePath}");
            if (!toAlbumDirInfo.Exists)
            {
                toAlbumDirInfo.Create();
                IncrementCounter(() => createdSubDirectories++);

                Console.WriteLine($"Created directory, {toAlbumDirInfo.FullName}");
            }

            UpdateArtistTag(updatedArtistName, tagFile, oldArtistName, artist, fromFile);
            fromFile.MoveTo(newFromFilePath, true);
            RemoveCacheByPath(newFromFilePath);

            IncrementCounter(() => movedFiles++);
        }
        else if (similarFiles.Count == 1 && _options.DeleteDuplicateFrom)
        {
            var similarFile = similarFiles.First();

            bool isFromHighQuality = HigherQualityMediaExtensions.Any(ext => fromFile.Extension.Contains(ext));
            bool isSimilarLowerQuality = LowerQualityMediaExtensions.Any(ext => similarFile.File.Extension.Contains(ext));
            
            if ((fromFile.Length > similarFile.File.Length) ||
                (tagFile.BitRate > similarFile.MediaInfo.BitRate) ||
                (isFromHighQuality && isSimilarLowerQuality))
            {
                if (!toAlbumDirInfo.Exists)
                {
                    toAlbumDirInfo.Create();
                    IncrementCounter(() => createdSubDirectories++);
                    Debug.WriteLine($"Created directory, {toAlbumDirInfo.FullName}");
                }

                UpdateArtistTag(updatedArtistName, tagFile, oldArtistName, artist, fromFile);
                fromFile.MoveTo(newFromFilePath, true);
                
                Console.WriteLine($"Moved {fromFile} >> {newFromFilePath}");
                
                RemoveCacheByPath(newFromFilePath);
                RemoveCacheByPath(similarFile.File.FullName);

                if (similarFile.File.FullName != newFromFilePath)
                {
                    similarFile.File.Delete();
                    IncrementCounter(() => remoteDelete++);
                    Console.WriteLine($"Deleted {similarFile.File.FullName}");
                    Console.WriteLine($"Deleting duplicated file '{similarFile.File.FullName}'");
                }

                IncrementCounter(() => movedFiles++);

                Console.WriteLine($"Similar file found, overwriting target, From is bigger, {similarFiles.Count}, {artist}/{tagFile.Album}, {fromFile.FullName}");
            }
            else if (similarFile.File.Length == fromFile.Length)
            {
                fromFile.Delete();
                IncrementCounter(() => localDelete++);

                Console.WriteLine($"Similar file found, deleted from file, exact same size from/target, {similarFiles.Count}, {artist}/{tagFile.Album}, {fromFile.FullName}");
            }
            else if (similarFile.File.Length > fromFile.Length)
            {
                fromFile.Delete();
                IncrementCounter(() => localDelete++);

                Console.WriteLine($"Similar file found, deleted from file, Target is bigger, {similarFiles.Count}, {artist}/{tagFile.Album}, {fromFile.FullName}");
            }
            else
            {
                Console.WriteLine($"Similar file found {similarFiles.Count}, {artist}/{tagFile.Album}, {similarFile.File.Extension}");
            }
        }
        else if (similarFiles.Count >= 2 && _options.DeleteDuplicateFrom)
        {
            var sortedSimilarFiles = similarFiles.OrderByDescending(file => file.File.Length);
            bool localIsBigger = sortedSimilarFiles.All(similarFile => fromFile.Length > similarFile.File.Length || tagFile.BitRate > similarFile.MediaInfo.BitRate);

            bool isFromHighQuality = HigherQualityMediaExtensions.Any(ext => fromFile.Extension.Contains(ext));
            bool isSimilarLowerQuality = LowerQualityMediaExtensions.Any(ext => similarFiles.Any(similarFile => similarFile.File.Extension.Contains(ext)));
            
            if (localIsBigger ||
                (isFromHighQuality && isSimilarLowerQuality))
            {
                if (!toAlbumDirInfo.Exists)
                {
                    toAlbumDirInfo.Create();
                    IncrementCounter(() => createdSubDirectories++);
                    Debug.WriteLine($"Created directory, {toAlbumDirInfo.FullName}");
                }

                UpdateArtistTag(updatedArtistName, tagFile, oldArtistName, artist, fromFile);
                fromFile.MoveTo(newFromFilePath, true);
                Console.WriteLine($"Moved {fromFile} >> {newFromFilePath}");
                RemoveCacheByPath(newFromFilePath);
                
                similarFiles.ForEach(similarFile =>
                {
                    if (similarFile.File.FullName != newFromFilePath)
                    {
                        Console.WriteLine($"Deleted {similarFile.File.FullName}");
                        Console.WriteLine($"Deleting duplicated file '{similarFile.File.FullName}'");

                        RemoveCacheByPath(similarFile.File.FullName);
                        
                        similarFile.File.Delete();
                        IncrementCounter(() => remoteDelete++);
                    }
                });

                IncrementCounter(() => movedFiles++);

                Console.WriteLine($"Similar files found, overwriting target, From is bigger, {similarFiles.Count}, {artist}/{tagFile.Album}, {fromFile.FullName}");
            }
            else
            {
                fromFile.Delete();
                IncrementCounter(() => localDelete++);
                Console.WriteLine($"Similar files found, deleted from file, Targets are bigger, {similarFiles.Count}, {artist}/{tagFile.Album}, {fromFile.FullName}");
            }

            Console.WriteLine($"Similar files found {similarFiles.Count}, {artist}/{tagFile.Album}");
        }

        return true;
    }

    private void RemoveCacheByPath(string fullPath)
    {
        lock (_memoryCache)
        {
            var cachedMediaInfo = _memoryCache.Get(fullPath) as CachedMediaInfo;
            if (cachedMediaInfo != null)
            {
                _memoryCache.Remove(fullPath, CacheEntryRemovedReason.Removed);
            }
        }
    }

    private List<SimilarFileInfo> GetSimilarFileFromTagsArtist(MediaFileInfo matchTagFile, FileInfo fromFileInfo,
        DirectoryInfo toArtistDirInfo, string artistName, out bool errors)
    {
        errors = false;
        List<SimilarFileInfo> tagFiles = new List<SimilarFileInfo>();
        FileInfo[] toFiles = toArtistDirInfo
            .GetFiles("*.*", SearchOption.AllDirectories)
            .Where(file => file.Length > 0)
            .Where(file => MediaFileExtensions.Any(ext => file.Name.ToLower().EndsWith(ext)))
            .ToArray();

        foreach (FileInfo toFile in toFiles)
        {
            try
            {
                if (toFile.FullName == fromFileInfo.FullName)
                {
                    continue;
                }
                
                CachedMediaInfo? cachedMediaInfo = null;

                lock (_memoryCache)
                {
                    cachedMediaInfo = _memoryCache.Get(toFile.FullName) as CachedMediaInfo;
                }

                if (cachedMediaInfo == null)
                {
                    IncrementCounter(() => scannedTargetFiles++);

                    try
                    {
                        cachedMediaInfo = new CachedMediaInfo(toFile);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"File gave read error, '{toFile.FullName}', {e.Message}");
                        continue;
                    }

                    try
                    {
                        if (cachedMediaInfo is null &&
                            _options.FixFileCorruption &&
                            _corruptionFixer.FixCorruption(toFile))
                        {
                            cachedMediaInfo = new CachedMediaInfo(toFile);

                            IncrementCounter(() => fixedCorruptedFiles++);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"File gave read error, '{toFile.FullName}', {e.Message}");
                        continue;
                    }

                    if (cachedMediaInfo is null)
                    {
                        continue;
                    }

                    lock (_memoryCache)
                    {
                        //Debug.WriteLine($"Added to cache {toFile.FullName}");
                        _memoryCache.Add(toFile.FullName, cachedMediaInfo, DateTimeOffset.Now.AddMinutes(CacheTime));
                    }
                }
                else
                {
                    IncrementCounter(() => cachedReadTargetFiles++);
                }

                if (string.IsNullOrWhiteSpace(cachedMediaInfo.Title) || 
                    string.IsNullOrWhiteSpace(cachedMediaInfo.Album))
                {
                    Console.WriteLine($"scan error file, no Title or Album: {toFile}");
                    errors = true;
                    if (!_options.ContinueScanError)
                    {
                        continue;
                    }
                }
                
                if (cachedMediaInfo?.Title.ToLower() == matchTagFile.Title.ToLower() &&
                    cachedMediaInfo?.Album.ToLower() == matchTagFile.Album.ToLower() &&
                    (cachedMediaInfo?.Track == matchTagFile.Track ||
                     cachedMediaInfo?.TrackCount == matchTagFile.TrackCount) &&
                    (cachedMediaInfo?.AlbumArtist?.ToLower() == matchTagFile.AlbumArtist?.ToLower() ||
                     cachedMediaInfo?.Artist?.ToLower() == matchTagFile.Artist?.ToLower() ||
                     GetUncoupledArtistName(cachedMediaInfo?.AlbumArtist?.ToLower()) == GetUncoupledArtistName(matchTagFile.AlbumArtist?.ToLower()) ||
                     GetUncoupledArtistName(cachedMediaInfo?.Artist?.ToLower()) == GetUncoupledArtistName(matchTagFile.Artist?.ToLower())))
                {
                    tagFiles.Add(new SimilarFileInfo(toFile, cachedMediaInfo));
                }
                else if (!string.IsNullOrWhiteSpace(cachedMediaInfo?.AcoustIdFingerPrint) && 
                         !string.IsNullOrWhiteSpace(matchTagFile.AcoustIdFingerPrint) &&
                         cachedMediaInfo?.AcoustIdFingerPrint == matchTagFile.AcoustIdFingerPrint)
                {
                    tagFiles.Add(new SimilarFileInfo(toFile, cachedMediaInfo));
                }
                if (string.Equals(toFile.Name.ToLower().Replace(toFile.Extension, string.Empty),
                             fromFileInfo.Name.ToLower().Replace(fromFileInfo.Extension, string.Empty),
                             StringComparison.CurrentCultureIgnoreCase))
                {
                    tagFiles.Add(new SimilarFileInfo(toFile, cachedMediaInfo));
                }
            }
            catch (Exception e)
            {
                errors = true;
                Console.WriteLine($"scan error file, {e.Message}, {toFile.FullName}");
            }
        }

        return tagFiles;
    }

    private void UpdateArtistTag(bool updatedArtistName, MediaFileInfo tagFile, string oldArtistName, string artist,
        FileInfo fromFile)
    {
        if (updatedArtistName && _options.UpdateArtistTags)
        {
            tagFile.Save(artist);
            
            IncrementCounter(() => updatedTagfiles++);
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
        Console.WriteLine($"Stats: Moved {movedFiles}, " +
                          $"Local Delete: {localDelete}, " +
                          $"Remote Delete: {remoteDelete}, " +
                          $"Fixed Corrupted: {fixedCorruptedFiles}, " +
                          $"Updated Artist Tags: {updatedTagfiles}, " +
                          $"Created SubDirectories: {createdSubDirectories}, " +
                          $"Scanned From: {scannedFromFiles}. " +
                          $"Cached Read Target: {cachedReadTargetFiles}. " +
                          $"Scanned Target: {scannedTargetFiles}. " +
                          $"Skipped Error: {skippedErrorFiles}, " +
                          $"Running: {(int)runtimeSw.Elapsed.TotalMinutes}:{runtimeSw.Elapsed.Seconds}");
    }

    private void IncrementCounter(Action callback)
    {
        lock (CounterLock)
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

    private string? GetUncoupledArtistName(string? artist)
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
    private string ReplaceDirectorySeparators(string input, string seperator)
    {
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
}