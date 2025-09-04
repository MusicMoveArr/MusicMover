using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using MusicMover.Helpers;

namespace MusicMover;

[Command("", Description = "Move missing music to directories to complete your collection")]
public class CliCommands : ICommand
{
    public static bool Debug { get; private set; }

    [CommandOption("from",
        Description = "From the directory.",
        EnvironmentVariable = "MOVE_FROM",
        IsRequired = false)]
    public string From { get; set; } = string.Empty;
    
    [CommandOption("from-file", 
        Description = "Read file paths to process from a file.", 
        EnvironmentVariable = "MOVE_FROM_FILE",
        IsRequired = false)]
    public string FromFile { get; set; } = string.Empty;
    
    [CommandOption("target", 
        Description = "directory to move/copy files to.", 
        EnvironmentVariable = "MOVE_TARGET",
        IsRequired = true)]
    public string Target { get; set; }
    
    [CommandOption("dryrun", 
        Description = "Dry run, no files are moved/copied.", 
        EnvironmentVariable = "MOVE_DRYRUN",
        IsRequired = false)]
    public bool Dryrun { get; set; }
    
    [CommandOption("create-artist-directory", 
        Description = "Create Artist directory if missing on target directory.", 
        EnvironmentVariable = "MOVE_CREATEARTISTDIRECTORY",
        IsRequired = false)]
    public bool CreateArtistDirectory { get; set; }
    
    [CommandOption("create-album-directory", 
        Description = "Create Album directory if missing on target directory.", 
        EnvironmentVariable = "MOVE_CREATEALBUMDIRECTORY",
        IsRequired = false)]
    public bool CreateAlbumDirectory { get; set; }
    
    [CommandOption("parallel", 
        Description = "multi-threaded processing.", 
        EnvironmentVariable = "MOVE_PARALLEL",
        IsRequired = false)]
    public bool Parallel { get; set; }
    
    [CommandOption("skip-directories", 
        Description = "Skip X amount of directories in the From directory to process.", 
        EnvironmentVariable = "MOVE_SKIPDIRECTORIES",
        IsRequired = false)]
    public int SkipDirectories { get; set; }
    
    [CommandOption("delete-duplicate-from", 
        Description = "Delete the song in From Directory if already found at Target.", 
        EnvironmentVariable = "MOVE_DELETEDUPLICATEFROM",
        IsRequired = false)]
    public bool DeleteDuplicateFrom { get; set; }
    
    [CommandOption("delete-duplicate-to", 
        Description = "Delete the song in To Directory if already found at Target (duplicates).", 
        EnvironmentVariable = "MOVE_DELETEDUPLICATETO",
        IsRequired = false)]
    public bool DeleteDuplicateTo { get; set; }
    
    [CommandOption("extra-scans", 
        Description = "Scan extra directories, usage, \"a\",\"b\", besides the target directory.", 
        EnvironmentVariable = "MOVE_EXTRASCANS",
        IsRequired = false)]
    public List<string> ExtraScans { get; set; }
    
    [CommandOption("extra-scan", 
        Description = "Scan a extra directory, besides the target directory.", 
        EnvironmentVariable = "MOVE_EXTRASCAN",
        IsRequired = false)]
    public string? ExtraScan { get; set; }
    
    [CommandOption("various-artists", 
        Description = "Rename \"Various Artists\" in the file name with First Performer.", 
        EnvironmentVariable = "MOVE_VARIOUSARTISTS",
        IsRequired = false)]
    public bool VariousArtists { get; set; }
    
    [CommandOption("extra-dir-must-exist", 
        Description = "Artist folder must already exist in the extra scanned directories.", 
        EnvironmentVariable = "MOVE_EXTRADIRMUSTEXIST",
        IsRequired = false)]
    public bool ExtraDirMustExist { get; set; }
    
    [CommandOption("artist-dirs-must-not-exist", 
        Description = "Artist folder must not exist in the extra scanned directories, only meant for --create-artist-directory", 
        EnvironmentVariable = "MOVE_EXTRADIRMUSTNOTEXIST",
        IsRequired = false)]
    public List<string> ArtistDirsMustNotExist { get; set; }
    
    [CommandOption("update-artist-tags", 
        Description = "Update Artist metadata tags", 
        EnvironmentVariable = "MOVE_UPDATEARTISTTAGS",
        IsRequired = false)]
    public bool UpdateArtistTags { get; set; }
    
    [CommandOption("fix-file-corruption", 
        Description = "Attempt fixing file corruption by using FFMpeg for from/target/scan files.", 
        EnvironmentVariable = "MOVE_FIXFILECORRUPTION",
        IsRequired = false)]
    public bool FixFileCorruption { get; set; }
    
    [CommandOption("acoustid-api-key", 
        Description = "When AcoustId API Key is set, try getting the artist/album/title when needed.", 
        EnvironmentVariable = "MOVE_ACOUSTIDAPIKEY",
        IsRequired = false)]
    public string AcoustidApiKey { get; set; }
    
    [CommandOption("file-format", 
        Description = "rename file format {Artist} {SortArtist} {Title} {Album} {Track} {TrackCount} {AlbumArtist} {AcoustId} {AcoustIdFingerPrint} {BitRate}.", 
        EnvironmentVariable = "MOVE_FILEFORMAT",
        IsRequired = false)]
    public string FileFormat { get; set; }

    [CommandOption("directory-seperator",
        Description = "Directory Seperator replacer, replace '/' '\\' to .e.g. '_'.",
        EnvironmentVariable = "MOVE_DIRECTORYSEPERATOR",
        IsRequired = false)]
    public string DirectorySeperator { get; set; } = "_";
    
    [CommandOption("always-check-acoustid", 
        Description = "Always check & Write to media with AcoustId for missing tags.", 
        EnvironmentVariable = "MOVE_ALWAYSCHECKACOUSTID",
        IsRequired = false)]
    public bool AlwaysCheckAcoustId { get; set; }
    
    [CommandOption("continue-scan-error", 
        Description = "Continue on scan errors from the Music Libraries.", 
        EnvironmentVariable = "MOVE_CONTINUESCANERROR",
        IsRequired = false)]
    public bool ContinueScanError { get; set; }
    
    [CommandOption("overwrite-artist", 
        Description = "Overwrite the Artist name when tagging from MusicBrainz.", 
        EnvironmentVariable = "MOVE_OVERWRITEARTIST",
        IsRequired = false)]
    public bool OverwriteArtist { get; set; }
    
    [CommandOption("overwrite-album-artist", 
        Description = "Overwrite the Album Artist name when tagging from MusicBrainz.", 
        EnvironmentVariable = "MOVE_OVERWRITEALBUMARTIST",
        IsRequired = false)]
    public bool OverwriteAlbumArtist { get; set; }
    
    [CommandOption("overwrite-album", 
        Description = "Overwrite the Album name when tagging from MusicBrainz.", 
        EnvironmentVariable = "MOVE_OVERWRITEALBUM",
        IsRequired = false)]
    public bool OverwriteAlbum { get; set; }
    
    [CommandOption("overwrite-track", 
        Description = "Overwrite the Track name when tagging from MusicBrainz.", 
        EnvironmentVariable = "MOVE_OVERWRITETRACK",
        IsRequired = false)]
    public bool OverwriteTrack { get; set; }
    
    [CommandOption("only-move-when-tagged", 
        Description = "Only process/move the media after it was MusicBrainz or Tidal tagged.", 
        EnvironmentVariable = "MOVE_ONLYMOVEWHENTAGGED",
        IsRequired = false)]
    public bool OnlyMoveWhenTagged { get; set; }
    
    [CommandOption("only-filename-matching", 
        Description = "Only filename matching when trying to find duplicates.", 
        EnvironmentVariable = "MOVE_ONLYFILEMATCHING",
        IsRequired = false)]
    public bool OnlyFileNameMatching { get; set; }
    
    [CommandOption("search-by-tag-names", 
        Description = "Search MusicBrainz from media tag-values if AcoustId matching failed.", 
        EnvironmentVariable = "MOVE_SEARCHBYTAGNAMES",
        IsRequired = false)]
    public bool SearchByTagNames { get; set; }
    
    [CommandOption("tidal-clientid", 
        Description = "The Client Id used for Tidal's API.", 
        EnvironmentVariable = "MOVE_TIDALCLIENTID",
        IsRequired = false)]
    public string TidalClientId { get; set; }
    
    [CommandOption("tidal-client-secret", 
        Description = "The Client Client used for Tidal's API.", 
        EnvironmentVariable = "MOVE_TIDALCLIENTSECRET",
        IsRequired = false)]
    public string TidalClientSecret { get; set; }

    [CommandOption("tidal-country-code",
        Description = "Tidal's CountryCode (e.g. US, FR, NL, DE etc).",
        EnvironmentVariable = "MOVE_TIDALCOUNTRYCODE",
        IsRequired = false)]
    public string TidalCountryCode { get; set; } = "US";

    [CommandOption("metadata-api-base-url",
        Description = "MiniMedia's Metadata API Base Url.",
        EnvironmentVariable = "MOVE_METADATAAPIBASEURL",
        IsRequired = false)]
    public string MetadataApiBaseUrl { get; set; } = string.Empty;
    
    [CommandOption("metadata-api-providers", 
        Description = "MiniMedia's Metadata API Provider (Any, Deezer, MusicBrainz, Spotify, Tidal).", 
        EnvironmentVariable = "MOVE_METADATAAPIPROVIDERS",
        IsRequired = false)]
    public List<string> MetadataApiProviders { get; set; }

    [CommandOption("preferred-file-extensions",
        Description = "The preferred music file extensions to use for your library (opus, m4a, flac etc with out '.').",
        EnvironmentVariable = "MOVE_PREFERREDFILEEXTENSIONS",
        IsRequired = false)]
    public List<string> PreferredFileExtensions { get; set; } = MoveProcessor.MediaFileExtensions.ToList();

    [CommandOption("debug",
        Description = "Show more detailed information in the console.",
        EnvironmentVariable = "MOVE_DEBUG",
        IsRequired = false)]
    public bool DebugInfo { get; set; } = false;

    [CommandOption("metadata-api-match-percentage",
        Description = "The percentage used for tagging, how accurate it must match with the remote metadata server.",
        EnvironmentVariable = "MOVE_METADATA_MATCH_PERCENTAGE",
        IsRequired = false)]
    public int MetadataApiMatchPercentage { get; set; } = 80;

    [CommandOption("tidal-match-percentage",
        Description = "The percentage used for tagging, how accurate it must match with Tidal.",
        EnvironmentVariable = "MOVE_TIDAL_MATCH_PERCENTAGE",
        IsRequired = false)]
    public int TidalMatchPercentage { get; set; } = 80;

    [CommandOption("musicbrainz-match-percentage",
        Description = "The percentage used for tagging, how accurate it must match with MusicBrainz.",
        EnvironmentVariable = "MOVE_MUSICBRAINZ_MATCH_PERCENTAGE",
        IsRequired = false)]
    public int MusicBrainzMatchPercentage { get; set; } = 80;

    [CommandOption("acoustid-match-percentage",
        Description = "The percentage used for tagging, how accurate it must match with AcoustId.",
        EnvironmentVariable = "MOVE_ACOUSTID_MATCH_PERCENTAGE",
        IsRequired = false)]
    public int AcoustIdMatchPercentage { get; set; } = 80;

    [CommandOption("trust-acoustid-when-tagging-failed",
        Description = "Put the trust into AcoustId when tagging failed completely.",
        EnvironmentVariable = "MOVE_TRUST_ACOUSTID_WHEN_TAGGING_FAILED",
        IsRequired = false)]
    public bool TrustAcoustIdWhenTaggingFailed { get; set; } = false;

    [CommandOption("move-untaggable-files-path",
        Description = "Move untaggable files (failed to tag by MusicBrainz, AcoustId, Spotify etc) to a specific folder.",
        EnvironmentVariable = "MOVE_MOVE_UNTAGGABLE_FILES_PATH",
        IsRequired = false)]
    public string MoveUntaggableFilesPath { get; set; }
    
    public async ValueTask ExecuteAsync(IConsole console)
    {
        CliCommands.Debug = DebugInfo;

        if (string.IsNullOrWhiteSpace(From) && 
            string.IsNullOrWhiteSpace(FromFile))
        {
            Logger.WriteLine("Missing From/FromFile configuration");
            return;
        }
        
        if (!Target.EndsWith('/'))
        {
            Target += '/';
        }
        if (!String.IsNullOrWhiteSpace(From) && !From.EndsWith('/'))
        {
            From += '/';
        }

        CliOptions options = new CliOptions
        {
            FromDirectory = From,
            FromFile = FromFile,
            ToDirectory = Target,
            AcoustIdApiKey = AcoustidApiKey,
            FileFormat = FileFormat,
            DirectorySeperator = DirectorySeperator,
            TidalClientId = TidalClientId,
            TidalClientSecret = TidalClientSecret,
            TidalCountryCode = TidalCountryCode,
            MetadataApiBaseUrl = MetadataApiBaseUrl,
            MetadataApiProviders = MetadataApiProviders,
            CreateAlbumDirectory = CreateAlbumDirectory,
            CreateArtistDirectory = CreateArtistDirectory,
            Parallel = Parallel,
            SkipFromDirAmount = SkipDirectories,
            DeleteDuplicateFrom = DeleteDuplicateFrom,
            DeleteDuplicateTo = DeleteDuplicateTo,
            RenameVariousArtists = VariousArtists,
            ExtraDirMustExist = ExtraDirMustExist,
            UpdateArtistTags = UpdateArtistTags,
            FixFileCorruption = FixFileCorruption,
            AlwaysCheckAcoustId = AlwaysCheckAcoustId,
            ContinueScanError = ContinueScanError,
            OverwriteArtist = OverwriteArtist,
            OverwriteAlbumArtist = OverwriteAlbumArtist,
            OverwriteAlbum = OverwriteAlbum,
            OverwriteTrack = OverwriteTrack,
            OnlyMoveWhenTagged = OnlyMoveWhenTagged,
            OnlyFileNameMatching = OnlyFileNameMatching,
            SearchByTagNames = SearchByTagNames,
            PreferredFileExtensions = PreferredFileExtensions,
            NonPreferredFileExtensions = MoveProcessor.MediaFileExtensions
                .Where(mediaExt => !PreferredFileExtensions.Any(ext => string.Equals(ext, mediaExt)))
                .ToList(),
            DebugInfo = DebugInfo,
            MetadataApiMatchPercentage = MetadataApiMatchPercentage,
            TidalMatchPercentage = TidalMatchPercentage,
            MusicBrainzMatchPercentage = MusicBrainzMatchPercentage,
            AcoustIdMatchPercentage = AcoustIdMatchPercentage,
            TrustAcoustIdWhenTaggingFailed = TrustAcoustIdWhenTaggingFailed,
            MoveUntaggableFilesPath = MoveUntaggableFilesPath
        };

        if (!string.IsNullOrWhiteSpace(MoveUntaggableFilesPath) && !Directory.Exists(MoveUntaggableFilesPath))
        {
            Logger.WriteLine($"Directory does not exist '{MoveUntaggableFilesPath}'");
            return;
        }
        if (!String.IsNullOrWhiteSpace(MoveUntaggableFilesPath) && !MoveUntaggableFilesPath.EndsWith('/'))
        {
            MoveUntaggableFilesPath += '/';
        }
        

        string[] supportedProviderTypes = [ "Any", "Deezer", "MusicBrainz", "Spotify", "Tidal" ];
        if (!string.IsNullOrWhiteSpace(MetadataApiBaseUrl) && (MetadataApiProviders?.Count == 0 ||
            !MetadataApiProviders?.Any(provider => supportedProviderTypes.Contains(provider)) == true))
        {
            Logger.WriteLine("No provider type selected for --metadata-api-providers / -MP variable");
            return;
        }
        
        Logger.WriteLine("Options used:");
        Logger.WriteLine($"From Directory: {options.FromDirectory}");
        Logger.WriteLine($"From File: {options.FromFile}");
        Logger.WriteLine($"ToDirectory: {options.ToDirectory}");
        Logger.WriteLine($"Create Album Directory: {options.CreateAlbumDirectory}");
        Logger.WriteLine($"Create Artist Directory: {options.CreateArtistDirectory}");
        Logger.WriteLine($"Parallel: {options.Parallel}");
        Logger.WriteLine($"Skip From Directory Amount: {options.SkipFromDirAmount}");
        Logger.WriteLine($"Delete Duplicate From: {options.DeleteDuplicateFrom}");
        Logger.WriteLine($"Delete Duplicate To: {options.DeleteDuplicateTo}");
        Logger.WriteLine($"Rename Various Artists: {options.RenameVariousArtists}");
        Logger.WriteLine($"Extra Directory Must Exist: {options.ExtraDirMustExist}");
        Logger.WriteLine($"Update Artist Tags: {options.UpdateArtistTags}");
        Logger.WriteLine($"Fix File Corruption: {options.FixFileCorruption}");
        Logger.WriteLine($"AcoustIdAPIKey: {options.AcoustIdApiKey}");
        Logger.WriteLine($"fileFormat: {options.FileFormat}");
        Logger.WriteLine($"Always Check AcoustId: {options.AlwaysCheckAcoustId}");
        Logger.WriteLine($"Overwrite Artist: {options.OverwriteArtist}");
        Logger.WriteLine($"Overwrite Album Artist: {options.OverwriteAlbumArtist}");
        Logger.WriteLine($"Overwrite Album: {options.OverwriteAlbum}");
        Logger.WriteLine($"Overwrite Track: {options.OverwriteTrack}");
        Logger.WriteLine($"Only Move When Tagged: {options.OnlyMoveWhenTagged}");
        Logger.WriteLine($"Only FileName Matching: {options.OnlyFileNameMatching}");
        Logger.WriteLine($"Search By Tag Names: {options.SearchByTagNames}");
        Logger.WriteLine($"Metadata API Base Url: {options.MetadataApiBaseUrl}");
        Logger.WriteLine($"metadata API Provider: {string.Join(',', options?.MetadataApiProviders ?? [])}");
        Logger.WriteLine($"preferred file extensions: {string.Join(',', options?.PreferredFileExtensions ?? [])}");
        Logger.WriteLine($"MetadataApi Match Percentage: {options.MetadataApiMatchPercentage}");
        Logger.WriteLine($"Tidal Match Percentage: {options.TidalMatchPercentage}");
        Logger.WriteLine($"MusicBrainz Match Percentage: {options.MusicBrainzMatchPercentage}");
        Logger.WriteLine($"AcoustId Match Percentage: {options.AcoustIdMatchPercentage}");

        if (ExtraScans?.Count > 0)
        {
            foreach (string extraDir in ExtraScans)
            {
                string extra = extraDir;
                if (!extra.EndsWith('/'))
                {
                    extra += '/';
                }
                options?.ExtraScans.Add(extra);
                Logger.WriteLine($"Extra scans, {extra}");
            }
        }
        
        if (ArtistDirsMustNotExist?.Count > 0)
        {
            foreach (string artistDir in ArtistDirsMustNotExist)
            {
                string directory = artistDir;
                if (!directory.EndsWith('/'))
                {
                    directory += '/';
                }
                options?.ArtistDirsMustNotExist.Add(directory);
                Logger.WriteLine($"Artist Directories Must Not Exist, {directory}");
            }
        }

        if (!string.IsNullOrWhiteSpace(ExtraScan))
        {
            if (!ExtraScan.EndsWith('/'))
            {
                ExtraScan += '/';
            }
            options?.ExtraScans.Add(ExtraScan);
            Logger.WriteLine($"Extra scan, {ExtraScan}");
        }
        
        MoveProcessor moveProcessor = new MoveProcessor(options!);

        if (!string.IsNullOrWhiteSpace(options?.FileFormat))
        {
            MediaFileInfo mediaFileInfo = new MediaFileInfo();
            mediaFileInfo.Artist = "Music";
            mediaFileInfo.SortArtist = "Music";
            mediaFileInfo.Title = "SomeTrack";
            mediaFileInfo.Album = "Mover";
            mediaFileInfo.Track = 5;
            mediaFileInfo.TrackCount = 15;
            mediaFileInfo.AlbumArtist = "MusicMover";
            mediaFileInfo.BitRate = 320;
            mediaFileInfo.Disc = 10;

            if (!TestFileFormatOutput(moveProcessor, mediaFileInfo, options))
            {
                return;
            }
            
            //test again but just 1 disc
            mediaFileInfo.Disc = 1;
            
            if (!TestFileFormatOutput(moveProcessor, mediaFileInfo, options))
            {
                return;
            }
        }
        moveProcessor.LoadPlugins();
        await moveProcessor.ProcessAsync();
    }

    private static bool TestFileFormatOutput(MoveProcessor moveProcessor, MediaFileInfo fileInfo, CliOptions options)
    {
        string[] invalidCharacters = ["?", "<", ">", "=", "{", "}"];
            
        string newFileName = moveProcessor.GetFormatName(fileInfo, options.FileFormat, options.DirectorySeperator);
        if (invalidCharacters.Any(invalidChar => newFileName.Contains(invalidChar)))
        {
            Logger.WriteLine($"FileFormat is incorrect, sample output: {newFileName}");
            return false;
        }

        return true;
    }
}