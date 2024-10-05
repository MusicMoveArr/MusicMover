using System.Diagnostics;
using System.Runtime.Caching;
using TagLib;
using File = TagLib.File;

namespace MusicMover;

public class MoveProcessor
{
    private const int CacheTime = 5; //minutes
    private const string VariousArtistsName = "Various Artists";
    private int movedFiles = 0;
    private int localDelete = 0;
    private int createdSubDirectories = 0;
    private int scannedFromFiles = 0;
    private int skippedErrorFiles = 0;
    private int scannedTargetFiles = 0;
    private int cachedReadTargetFiles = 0;
    private object CounterLock = new object();
    private int updatedTagfiles = 0;
    
    private Stopwatch sw = Stopwatch.StartNew();
    private Stopwatch runtimeSw = Stopwatch.StartNew();

    private CliOptions _options;
    private MemoryCache _memoryCache;
    
    public MoveProcessor(CliOptions options)
    {
        _options = options;
        _memoryCache = MemoryCache.Default;
    }
    
    public void Process()
    {
        var sortedTopDirectories = Directory
            .EnumerateFileSystemEntries(_options.FromDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(file => Directory.Exists(file))
            .Where(dir => !dir.Contains(".Trash"))
            //.Where(dir => Regex.IsMatch(new DirectoryInfo(dir).Name, "^[a-rA-R]{1}"))
            .OrderBy(dir => dir)
            .Skip(_options.SkipFromDirAmount)
            .ToList();

        if (_options.Parallel)
        {
            sortedTopDirectories
                .AsParallel()
                .ForAll(dir => ProcessDirectory(dir));
        }
        else
        {
            sortedTopDirectories
                .ForEach(dir => ProcessDirectory(dir));
        }
        
        ShowProgress();
    }
    
    private void ProcessDirectory(string fromTopDir)
    {
        DirectoryInfo fromDirInfo = new DirectoryInfo(fromTopDir);
        
        FileInfo[] flacFromFiles = fromDirInfo.GetFiles("*.*", SearchOption.AllDirectories);

        foreach (FileInfo fromFile in flacFromFiles)
        {
            lock (sw)
            {
                if (sw.Elapsed.Seconds >= 5)
                {
                    ShowProgress();
                    sw.Restart();
                }
            }

            lock (CounterLock)
            {
                scannedFromFiles++;
            }
            
            Debug.WriteLine($"File: {fromFile.FullName}");
            TagLib.File tagFile;

            try
            {
                tagFile = TagLib.File.Create(fromFile.FullName);
            }
            catch (Exception e)
            {
                lock (CounterLock)
                {
                    skippedErrorFiles++;
                }
                Console.WriteLine($"{e.Message}, {fromFile.FullName}");
                continue;
            }
            
            string oldArtistName = tagFile.Tag.FirstAlbumArtist;
            string artist = tagFile.Tag.FirstAlbumArtist;
            bool updatedArtistName = false;

            if (string.IsNullOrWhiteSpace(artist) ||
                (artist == VariousArtistsName && !string.IsNullOrWhiteSpace(tagFile.Tag.FirstPerformer)))
            {
                artist = tagFile.Tag.FirstPerformer;
            }

            if (string.IsNullOrWhiteSpace(artist) ||
                string.IsNullOrWhiteSpace(tagFile.Tag.Album) ||
                string.IsNullOrWhiteSpace(tagFile.Tag.Title))
            {
                Console.WriteLine($"File is missing Artist, Album or title in the tags, skipping, {fromFile.FullName}");
                continue;
            }
            
            DirectoryInfo toArtistDirInfo = new DirectoryInfo($"{_options.ToDirectory}{SanitizeArtistName(artist)}");
            DirectoryInfo toAlbumDirInfo = new DirectoryInfo($"{_options.ToDirectory}{SanitizeArtistName(artist)}/{SanitizeAlbumName(tagFile.Tag.Album)}");
            
            if (!toArtistDirInfo.Exists)
            {
                if (artist.Contains(','))
                {
                    artist = artist.Substring(0, artist.IndexOf(',')).Trim();
                    
                    updatedArtistName = true;
                    if (!SetToArtistDirectory(artist, tagFile, out toArtistDirInfo, out toAlbumDirInfo))
                    {
                        continue;
                    }
                }
                else if (artist.Contains('&'))
                {
                    artist = artist.Substring(0, artist.IndexOf('&')).Trim();
                    updatedArtistName = true;
                    if (!SetToArtistDirectory(artist, tagFile, out toArtistDirInfo, out toAlbumDirInfo))
                    {
                        continue;
                    }
                }
                else if (artist.Contains('+'))
                {
                    artist = artist.Substring(0, artist.IndexOf('+')).Trim();
                    updatedArtistName = true;
                    if (!SetToArtistDirectory(artist, tagFile, out toArtistDirInfo, out toAlbumDirInfo))
                    {
                        continue;
                    }
                }
                else if (artist.Contains('/'))
                {
                    artist = artist.Substring(0, artist.IndexOf('/')).Trim();
                    updatedArtistName = true;
                    if (!SetToArtistDirectory(artist, tagFile, out toArtistDirInfo, out toAlbumDirInfo))
                    {
                        continue;
                    }
                }
                else if (artist.Contains(" feat"))
                {
                    artist = artist.Substring(0, artist.IndexOf(" feat")).Trim();
                    updatedArtistName = true;
                    if (!SetToArtistDirectory(artist, tagFile, out toArtistDirInfo, out toAlbumDirInfo))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!SetToArtistDirectory(artist, tagFile, out toArtistDirInfo, out toAlbumDirInfo))
                    {
                        continue;
                    }
                }
            }

            if (!toArtistDirInfo.Exists)
            {
                continue;
            }

            bool scanErrors = false;
            
            
            
            List<SimilarFileInfo> similarFiles = GetSimilarFileFromTagsArtist(tagFile, fromFile, toArtistDirInfo, artist, out scanErrors);

            if (scanErrors)
            {
                Console.WriteLine($"Scan errors... skipping {fromFile.FullName}");
                continue;
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
                continue;
            }
            
            if (scanErrors)
            {
                Console.WriteLine($"Scan errors... skipping {fromFile.FullName}");
                continue;
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
                Debug.WriteLine($"No similar files found moving, {artist}/{tagFile.Tag.Album}, {newFromFilePath}");
                if (!toAlbumDirInfo.Exists)
                {
                    toAlbumDirInfo.Create();
                    lock (CounterLock)
                    {
                        createdSubDirectories++;
                    }
                    
                    Debug.WriteLine($"Created directory, {toAlbumDirInfo.FullName}");
                }

                UpdateArtistTag(updatedArtistName, tagFile, oldArtistName, artist, fromFile);
                
                fromFile.MoveTo(newFromFilePath, true);
                
                lock (CounterLock)
                {
                    movedFiles++;
                }
            }
            else if (similarFiles.Count == 1 && _options.DeleteDuplicateFrom)
            {
                var similarFile = similarFiles.First();
                if (similarFile.File.Length == fromFile.Length)
                {
                    fromFile.Delete();
                    lock (CounterLock)
                    {
                        localDelete++;
                    }
                    
                    Debug.WriteLine($"Similar files found, deleted from file, exact same size from/target, {similarFiles.Count}, {artist}/{tagFile.Tag.Album}, {fromFile.FullName}");
                }
                else if (similarFile.File.Length > fromFile.Length)
                {
                    fromFile.Delete();
                    lock (CounterLock)
                    {
                        localDelete++;
                    }
                    
                    Debug.WriteLine($"Similar files found, deleted from file, Target is bigger, {similarFiles.Count}, {artist}/{tagFile.Tag.Album}, {fromFile.FullName}");
                }
                else if (fromFile.Length > similarFile.File.Length)
                {
                    if (!toAlbumDirInfo.Exists)
                    {
                        toAlbumDirInfo.Create();
                        lock (CounterLock)
                        {
                            createdSubDirectories++;
                        }
                    
                        Debug.WriteLine($"Created directory, {toAlbumDirInfo.FullName}");
                    }
                    UpdateArtistTag(updatedArtistName, tagFile, oldArtistName, artist, fromFile);
                    fromFile.MoveTo(newFromFilePath, true);
                    similarFile.File.Delete();
                
                    lock (CounterLock)
                    {
                        movedFiles++;
                    }
                    
                    Debug.WriteLine($"Similar files found, overwriting target, From is bigger, {similarFiles.Count}, {artist}/{tagFile.Tag.Album}, {fromFile.FullName}");
                }
                
                //leaving this here for temp... maybe needed later?
                //else if (similarFile.Extension is ".m4a" or ".mp3")
                //{
                //    if (!toAlbumDirInfo.Exists)
                //    {
                //        toAlbumDirInfo.Create();
                //        lock (CounterLock)
                //        {
                //            createdSubDirectories++;
                //        }
                //        Debug.WriteLine($"Created directory, {toAlbumDirInfo.FullName}");
                //    }
                //    flacFromFile.MoveTo(newFlacFilePath, true);
                //    similarFile.Delete();
                //    lock (CounterLock)
                //    {
                //        movedFiles++;
                //    }
                //    
                //    Debug.WriteLine($"Similar files found, replaced file, {similarFiles.Count}, {artist}/{tagFile.Tag.Album},  {flacFromFile.FullName}");
                //}
                else
                {
                    Debug.WriteLine($"Similar files found {similarFiles.Count}, {artist}/{tagFile.Tag.Album}, {similarFile.File.Extension}");
                }
            }
            else
            {
                Debug.WriteLine($"Similar files found {similarFiles.Count}, {artist}/{tagFile.Tag.Album}");
            }
        }
    }

    private bool SetToArtistDirectory(string artist, File tagFile, 
        out DirectoryInfo toArtistDirInfo,
        out DirectoryInfo toAlbumDirInfo)
    {
        toArtistDirInfo = new DirectoryInfo($"{_options.ToDirectory}{SanitizeArtistName(artist)}");
        toAlbumDirInfo = new DirectoryInfo($"{_options.ToDirectory}{SanitizeArtistName(artist)}/{SanitizeAlbumName(tagFile.Tag.Album)}");

        if (!toArtistDirInfo.Exists && 
            _options.CreateArtistDirectory &&
            !_options.IsDryRun)
        {
            toArtistDirInfo.Create();
        }

        if (!toArtistDirInfo.Exists)
        {
            Debug.WriteLine($"Artist does {artist} not exist");
        }

        return toArtistDirInfo.Exists;
    }
    
    private List<SimilarFileInfo> GetSimilarFileFromTagsArtist(TagLib.File matchTagFile, FileInfo fromFileInfo, DirectoryInfo toArtistDirInfo, string artistName, out bool errors)
    {
        errors = false;
        List<SimilarFileInfo> tagFiles = new List<SimilarFileInfo>();
        FileInfo[] toFiles = toArtistDirInfo.GetFiles("*.*", SearchOption.AllDirectories);
        
        foreach (FileInfo toFile in toFiles)
        {
            try
            {
                TagLib.File? tagFile = null;
                
                lock (_memoryCache)
                {
                    tagFile = _memoryCache.Get(toFile.FullName) as TagLib.File;
                }

                if (tagFile == null)
                {
                    lock (CounterLock)
                    {
                        scannedTargetFiles++;
                    }
                    tagFile = TagLib.File.Create(toFile.FullName);

                    lock (_memoryCache)
                    {
                        _memoryCache.Add(toFile.FullName, tagFile, DateTimeOffset.Now.AddMinutes(CacheTime));
                    }
                }
                else
                {
                    lock (CounterLock)
                    {
                        cachedReadTargetFiles++;
                    }
                }

                if (tagFile.Tag.Title == matchTagFile.Tag.Title &&
                    tagFile.Tag.Album == matchTagFile.Tag.Album &&
                    ((tagFile.Tag.FirstAlbumArtist == matchTagFile.Tag.FirstAlbumArtist ||
                     tagFile.Tag.FirstPerformer == matchTagFile.Tag.FirstPerformer) ||
                    (tagFile.Tag.FirstAlbumArtist == artistName ||
                     tagFile.Tag.FirstPerformer == artistName)))
                {
                    tagFiles.Add(new SimilarFileInfo(toFile, tagFile));
                }
                else if(string.Equals(toFile.Name.Replace(toFile.Extension, string.Empty), 
                                      fromFileInfo.Name.Replace(fromFileInfo.Extension, string.Empty), 
                                      StringComparison.CurrentCultureIgnoreCase))
                {
                    tagFiles.Add(new SimilarFileInfo(toFile, tagFile));
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

    private void UpdateArtistTag(bool updatedArtistName, TagLib.File tagFile, string oldArtistName, string artist, FileInfo fromFile)
    {
        if (updatedArtistName && _options.UpdateArtistTags)
        {
            tagFile.Tag.AlbumArtists = new[] { artist };
            tagFile.Tag.Performers = new[] { artist };
            tagFile.Save();
            Console.WriteLine($"Updated {updatedTagfiles}, {oldArtistName} => {artist}, {fromFile.FullName}");
            lock (CounterLock)
            {
                updatedTagfiles++;
            }
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
                          $"Updated Artist Tags: {updatedTagfiles}, " +
                          $"created SubDirectories: {createdSubDirectories}, " +
                          $"Scanned From Files: {scannedFromFiles}. " +
                          $"Cached Read Target Files: {cachedReadTargetFiles}. " +
                          $"Scanned Target Files: {scannedTargetFiles}. " +
                          $"Skipped Error Files: {skippedErrorFiles}, " +
                          $"Running: {(int)runtimeSw.Elapsed.TotalMinutes}:{runtimeSw.Elapsed.Seconds}");
    }
}