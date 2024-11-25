using System.Diagnostics;
using System.Globalization;
using System.Runtime.Caching;
using ListRandomizer;
using TagLib;
using File = TagLib.File;

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
        toAlbumDirInfo = new DirectoryInfo($"{_options.ToDirectory}{SanitizeArtistName(artist)}/{SanitizeAlbumName(tagFile.TrackInfo.Album)}");

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
        }

        if (tagFile is null)
        {
            return false;
        }

        //return false;

        //new TagLib.File().Tag.fir
        
        string oldArtistName = tagFile.TrackInfo.AlbumArtist;
        string artist = tagFile.TrackInfo.AlbumArtist;
        bool updatedArtistName = false;

        if (string.IsNullOrWhiteSpace(artist) ||
            (artist.Contains(VariousArtistsName) && !string.IsNullOrWhiteSpace(tagFile.TrackInfo.Artist)))
        {
            artist = tagFile.TrackInfo.Artist;
        }
        
        if (string.IsNullOrWhiteSpace(artist) ||
            (artist.Contains(VariousArtistsName) && !string.IsNullOrWhiteSpace(tagFile.TrackInfo.SortArtist)))
        {
            artist = tagFile.TrackInfo.SortArtist;
        }

        if (string.IsNullOrWhiteSpace(artist) ||
            string.IsNullOrWhiteSpace(tagFile.TrackInfo.Album) ||
            string.IsNullOrWhiteSpace(tagFile.TrackInfo.Title))
        {
            Console.WriteLine($"File is missing Artist, Album or title in the tags, skipping, {fromFile.FullName}");
            return false;
        }

        DirectoryInfo toArtistDirInfo = new DirectoryInfo($"{_options.ToDirectory}{SanitizeArtistName(artist)}");
        DirectoryInfo toAlbumDirInfo = new DirectoryInfo($"{_options.ToDirectory}{SanitizeArtistName(artist)}/{SanitizeAlbumName(tagFile.TrackInfo.Album)}");

        
        
        //if (!toArtistDirInfo.Exists)
        //{
        string? newArtistName = GetUncoupledArtistName(artist);

        if (!string.IsNullOrWhiteSpace(newArtistName) && newArtistName.Length >= 3 && newArtistName != artist)
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
        //}

        if (!toArtistDirInfo.Exists)
        {
            return false;
        }

        bool scanErrors = false;

        List<SimilarFileInfo> similarFiles = GetSimilarFileFromTagsArtist(tagFile, fromFile, toArtistDirInfo, artist, out scanErrors);

        if (scanErrors)
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

            if (scanErrors)
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

        if (scanErrors)
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

        if (similarFiles.Count == 0)
        {
            Debug.WriteLine($"No similar files found moving, {artist}/{tagFile.TrackInfo.Album}, {newFromFilePath}");
            if (!toAlbumDirInfo.Exists)
            {
                toAlbumDirInfo.Create();
                IncrementCounter(() => createdSubDirectories++);

                Debug.WriteLine($"Created directory, {toAlbumDirInfo.FullName}");
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

            if (tagFile.TrackInfo.Bitrate > similarFile.MediaInfo.BitRate)
            {
                
            }
            
            if ((fromFile.Length > similarFile.File.Length) ||
                (tagFile.TrackInfo.Bitrate > similarFile.MediaInfo.BitRate) ||
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
                RemoveCacheByPath(newFromFilePath);
                RemoveCacheByPath(similarFile.File.FullName);

                if (similarFile.File.FullName != newFromFilePath)
                {
                    similarFile.File.Delete();
                    IncrementCounter(() => remoteDelete++);
                    Debug.WriteLine($"Deleting duplicated file '{similarFile.File.FullName}'");
                }

                IncrementCounter(() => movedFiles++);

                Debug.WriteLine($"Similar file found, overwriting target, From is bigger, {similarFiles.Count}, {artist}/{tagFile.TrackInfo.Album}, {fromFile.FullName}");
            }
            else if (similarFile.File.Length == fromFile.Length)
            {
                fromFile.Delete();
                IncrementCounter(() => localDelete++);

                Debug.WriteLine($"Similar file found, deleted from file, exact same size from/target, {similarFiles.Count}, {artist}/{tagFile.TrackInfo.Album}, {fromFile.FullName}");
            }
            else if (similarFile.File.Length > fromFile.Length)
            {
                fromFile.Delete();
                IncrementCounter(() => localDelete++);

                Debug.WriteLine($"Similar file found, deleted from file, Target is bigger, {similarFiles.Count}, {artist}/{tagFile.TrackInfo.Album}, {fromFile.FullName}");
            }
            else
            {
                Debug.WriteLine($"Similar file found {similarFiles.Count}, {artist}/{tagFile.TrackInfo.Album}, {similarFile.File.Extension}");
            }
        }
        else if (similarFiles.Count >= 2 && _options.DeleteDuplicateFrom)
        {
            var sortedSimilarFiles = similarFiles.OrderByDescending(file => file.File.Length);
            bool localIsBigger = sortedSimilarFiles.All(similarFile => fromFile.Length > similarFile.File.Length || tagFile.TrackInfo.Bitrate > similarFile.MediaInfo.BitRate);

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
                RemoveCacheByPath(newFromFilePath);
                
                similarFiles.ForEach(similarFile =>
                {
                    if (similarFile.File.FullName != newFromFilePath)
                    {
                        Debug.WriteLine($"Deleting duplicated file '{similarFile.File.FullName}'");

                        RemoveCacheByPath(similarFile.File.FullName);
                        
                        similarFile.File.Delete();
                        IncrementCounter(() => remoteDelete++);
                    }
                });

                IncrementCounter(() => movedFiles++);

                Debug.WriteLine($"Similar files found, overwriting target, From is bigger, {similarFiles.Count}, {artist}/{tagFile.TrackInfo.Album}, {fromFile.FullName}");
            }
            else
            {
                fromFile.Delete();
                IncrementCounter(() => localDelete++);
                Debug.WriteLine($"Similar files found, deleted from file, Targets are bigger, {similarFiles.Count}, {artist}/{tagFile.TrackInfo.Album}, {fromFile.FullName}");
            }

            Debug.WriteLine($"Similar files found {similarFiles.Count}, {artist}/{tagFile.TrackInfo.Album}");
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
                    catch (Exception exception)
                    {
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
                    Debug.WriteLine($"scan error file: {toFile}");
                    errors = true;
                    continue;
                }
                
                if (cachedMediaInfo.Title.ToLower() == matchTagFile.TrackInfo.Title.ToLower() &&
                    cachedMediaInfo.Album.ToLower() == matchTagFile.TrackInfo.Album.ToLower() &&
                    (cachedMediaInfo.Track == matchTagFile.TrackInfo.TrackNumber ||
                     cachedMediaInfo.TrackCount == matchTagFile.TrackInfo.TrackTotal) &&
                    
                    (cachedMediaInfo.FirstAlbumArtist?.ToLower() == matchTagFile.TrackInfo.AlbumArtist?.ToLower() ||
                     cachedMediaInfo.FirstPerformer?.ToLower() == matchTagFile.TrackInfo.Artist?.ToLower() ||
                     cachedMediaInfo.FirstAlbumArtist?.ToLower() == artistName.ToLower() ||
                     cachedMediaInfo.FirstPerformer?.ToLower() == artistName.ToLower() ||
                     
                     GetUncoupledArtistName(cachedMediaInfo.FirstAlbumArtist?.ToLower()) == GetUncoupledArtistName(matchTagFile.TrackInfo.AlbumArtist?.ToLower()) ||
                     GetUncoupledArtistName(cachedMediaInfo.FirstPerformer?.ToLower()) == GetUncoupledArtistName(matchTagFile.TrackInfo.Artist?.ToLower()) ||
                     GetUncoupledArtistName(cachedMediaInfo.FirstAlbumArtist?.ToLower()) == GetUncoupledArtistName(artistName.ToLower()) ||
                     GetUncoupledArtistName(cachedMediaInfo.FirstPerformer?.ToLower()) == GetUncoupledArtistName(artistName.ToLower())))
                {
                    tagFiles.Add(new SimilarFileInfo(toFile, cachedMediaInfo));
                }
                else if (!string.IsNullOrWhiteSpace(cachedMediaInfo.AcoustIdFingerPrint) && !string.IsNullOrWhiteSpace(matchTagFile.AcoustIdFingerPrint) &&
                         cachedMediaInfo.AcoustIdFingerPrint == matchTagFile.AcoustIdFingerPrint)
                {
                    tagFiles.Add(new SimilarFileInfo(toFile, cachedMediaInfo));
                }
                //else if (!string.IsNullOrWhiteSpace(cachedMediaInfo.AcoustId) && !string.IsNullOrWhiteSpace(matchTagFile.AcoustId) &&
                //         cachedMediaInfo.AcoustId == matchTagFile.AcoustId)
                //{
                //    tagFiles.Add(new SimilarFileInfo(toFile, cachedMediaInfo));
                //}
                else if (string.Equals(toFile.Name.Replace(toFile.Extension, string.Empty),
                             fromFileInfo.Name.Replace(fromFileInfo.Extension, string.Empty),
                             StringComparison.CurrentCultureIgnoreCase))
                {
                    tagFiles.Add(new SimilarFileInfo(toFile, cachedMediaInfo));
                }
            }
            catch (Exception e)
            {
                errors = true;
                Console.WriteLine($"{e.Message}, {toFile.FullName}");
            }
        }

        return tagFiles;
    }

    private void UpdateArtistTag(bool updatedArtistName, MediaFileInfo tagFile, string oldArtistName, string artist,
        FileInfo fromFile)
    {
        if (updatedArtistName && _options.UpdateArtistTags)
        {
            tagFile.TrackInfo.AlbumArtist = artist;
            tagFile.TrackInfo.Artist = artist;
            tagFile.TrackInfo.Save();

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
    
    private string NormalizeText(string input)
    {
        // Words to exclude from capitalization (except if they're the first word)
        HashSet<string> smallWords = new HashSet<string> { "of", "the", "and", "in", "on", "at", "for", "to" };

        // Create a TextInfo object for title casing
        TextInfo textInfo = CultureInfo.InvariantCulture.TextInfo;

        // Split the string into words and delimiters
        var words = new List<string>();
        var delimiters = new List<char>();
        char[] separatorCharacters = { ':', '-', '_', ' ', '/' }; // Add more as needed

        int start = 0;
        for (int i = 0; i < input.Length; i++)
        {
            if (Array.Exists(separatorCharacters, c => c == input[i]))
            {
                // Add word and delimiter
                if (start < i)
                {
                    words.Add(input.Substring(start, i - start)); // Add word
                }
                delimiters.Add(input[i]); // Add delimiter
                start = i + 1;
            }
        }

        // Add the last word if any
        if (start < input.Length)
        {
            words.Add(input.Substring(start));
        }

        // Capitalize each word considering small words
        for (int i = 0; i < words.Count; i++)
        {
            string word = words[i].ToLower();
            if (i == 0 || !smallWords.Contains(word)) // Capitalize if first word or not a small word
            {
                words[i] = textInfo.ToTitleCase(word);
            }
        }

        // Reconstruct the string with original delimiters
        string result = "";
        int wordIndex = 0, delimiterIndex = 0;

        for (int i = 0; i < input.Length; i++)
        {
            if (delimiterIndex < delimiters.Count && input[i] == delimiters[delimiterIndex])
            {
                result += delimiters[delimiterIndex++];
            }
            else if (wordIndex < words.Count)
            {
                result += words[wordIndex++];
                i += words[wordIndex - 1].Length - 1; // Skip processed word
            }
        }

        return result;
    }
}