using System.Diagnostics;
using MusicMover.Helpers;
using MusicMover.MediaHandlers;
using MusicMover.Rules;
using MusicMover.Rules.Machine;
using MusicMoverPlugin;
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

    public static int MovedFiles = 0;
    public static int LocalDelete = 0;
    public static int RemoteDelete = 0;
    public static int CreatedSubDirectories = 0;
    public static int ScannedFromFiles = 0;
    public static int SkippedErrorFiles = 0;
    public static int ScannedTargetFiles = 0;
    public static int CachedReadTargetFiles = 0;
    public static int FixedCorruptedFiles = 0;
    public static int UpdatedTagfiles = 0;
    public static int ProcessedFiles = 0;
    
    
    private static object _counterLock = new object();
    private bool _exitProcess = false;

    private Stopwatch _sw = Stopwatch.StartNew();
    private Stopwatch _runtimeSw = Stopwatch.StartNew();
    private List<MediaHandler> _filesToProcess = new List<MediaHandler>();
    private const int BatchFileProcessing = 1000;

    private readonly CliOptions _options;
    private readonly List<IPlugin> _plugins = new List<IPlugin>();
    private List<string> _artistsNotFound = new List<string>();
    private List<string> _unprocessedArtists = new List<string>();

    public MoveProcessor(CliOptions options)
    {
        _options = options;
    }

    public void LoadPlugins()
    {
        _plugins.AddRange(PluginHelper.LoadPlugins());
    }

    public async Task ProcessAsync()
    {
        
        
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
                var totalProgressTask = ctx.AddTask(Markup.Escape($"Processing tracks {ProcessedFiles} of {_filesToProcess.Count + ProcessedFiles}"));
                totalProgressTask.MaxValue = _filesToProcess.Count;
                
                int maxDegreeOfParallelism = _options.Parallel ? 4 : 1;
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
                        IncrementCounter(() => SkippedErrorFiles++);
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
                    totalProgressTask.Description(Markup.Escape($"Processing tracks {totalProgressTask.Value + ProcessedFiles} of {_filesToProcess.Count + ProcessedFiles}, moved: {MovedFiles}, local delete: {LocalDelete}, remote delete: {RemoteDelete}"));
                });
            });

        ProcessedFiles += _filesToProcess.Count;
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
            MediaHandler? mediaFileInfo = await MediaFileHelper.GetMediaFileHandlerAsync(fromFile, _options);

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
            MediaHandler? mediaHandler = await MediaFileHelper.GetMediaFileHandlerAsync(fileInfo, _options);

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

    private async Task<bool> ProcessFromFileAsync(MediaHandler mediaHandler)
    {
        IncrementCounter(() => ScannedFromFiles++);
        Debug.WriteLine($"File: {mediaHandler.FileInfo.FullName}");
        
        SimpleRuleEngine ruleEngine = new SimpleRuleEngine();
        ruleEngine.AddRule<FileExistsRule>();
        ruleEngine.AddRule<EnoughDiskSpaceRule>();
        ruleEngine.AddRule<SetToArtistDirectoryRule>();
        ruleEngine.AddRule<TagFileAcoustIdRule>();
        ruleEngine.AddRule<TagFileMetadataApiRule>();
        ruleEngine.AddRule<TagFileTidalRule>();
        ruleEngine.AddRule<TagFileFailedRule>();
        ruleEngine.AddRule<OnlyMoveWhenTaggedRule>();
        ruleEngine.AddRule<OnlyMoveWhenTaggedMoveFileRule>();
        ruleEngine.AddRule<SetTargetFilePathRule>();
        ruleEngine.AddRule<CheckRequiredTagsRule>();
        ruleEngine.AddRule<CheckSimilarFilesRule>();
        ruleEngine.AddRule<StopAtDryRunRule>();
        ruleEngine.AddRule<CreateArtistDirectoryRule>();
        ruleEngine.AddRule<MoveNewFileRule>();
        ruleEngine.AddRule<MoveOneSimilarFileRule>();
        ruleEngine.AddRule<MoveMultipleSimilarFilesRule>();
        
        await ruleEngine.RunAsync(new StateObject
        {
            MediaHandler = mediaHandler,
            Options = _options
        });

        
        return true;
    }

    private void ShowProgress()
    {
        Logger.WriteLine($"Stats: Moved {MovedFiles}, " +
                           (_options.DeleteDuplicateFrom ? $"Local Delete: {LocalDelete}, " : string.Empty) +
                           (_options.DeleteDuplicateTo ? $"Remote Delete: {RemoteDelete}, " : string.Empty) +
                           (_options.FixFileCorruption ? $"Fixed Corrupted: {FixedCorruptedFiles}, " : string.Empty) +
                           (_options.UpdateArtistTags ? $"Updated Artist Tags: {UpdatedTagfiles}, " : string.Empty) +
                           (_options.CreateAlbumDirectory ? $"Created SubDirectories: {CreatedSubDirectories}, " : string.Empty) +
                           $"Scanned From: {ScannedFromFiles}. " +
                           (!_options.OnlyFileNameMatching ? $"Cached Read Target: {CachedReadTargetFiles}. " : string.Empty) +
                           (!_options.OnlyFileNameMatching ? $"Scanned Target: {ScannedTargetFiles}. " : string.Empty) +
                           $"Skipped Error: {SkippedErrorFiles}, " +
                           $"Running: {_runtimeSw.Elapsed.Hours:D2}:{_runtimeSw.Elapsed.Minutes:D2}:{_runtimeSw.Elapsed.Seconds:D2}");
    }
    
    public static void IncrementCounter(Action callback)
    {
        lock (_counterLock)
        {
            callback();
        }
    }
}