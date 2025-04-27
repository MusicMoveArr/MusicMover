using ConsoleAppFramework;

namespace MusicMover;

public class CliCommands
{
    /// <summary>
    /// Music Mover, Move missing music to directories to complete your collection
    /// </summary>
    /// <param name="from">-f, From the directory.</param>
    /// <param name="target">-t, directory to move/copy files to.</param>
    /// <param name="dryrun">-d, Dry run, no files are moved/copied.</param>
    /// <param name="createArtistDirectory">-g, Create Artist directory if missing on target directory.</param>
    /// <param name="createAlbumDirectory">-u, Create Album directory if missing on target directory.</param>
    /// <param name="parallel">-p, multi-threaded processing.</param>
    /// <param name="skipDirectories">-s, Skip X amount of directories in the From directory to process.</param>
    /// <param name="deleteDuplicateFrom">-w, Delete the song in From Directory if already found at Target.</param>
    /// <param name="deleteDuplicateTo">-W, Delete the song in To Directory if already found at Target (duplicates).</param>
    /// <param name="extrascans">-A, Scan extra directories, usage, ["a","b"], besides the target directory.</param>
    /// <param name="extrascan">-a, Scan a extra directory, besides the target directory.</param>
    /// <param name="variousArtists">-va, Rename "Various Artists" in the file name with First Performer.</param>
    /// <param name="extraDirMustExist">-AX, Artist folder must already exist in the extra scanned directories.</param>
    /// <param name="artistDirsMustNotExist">-AN, Artist folder must not exist in the extra scanned directories, only meant for --createArtistDirectory, -g.</param>
    /// <param name="updateArtistTags">-UA, Update Artist metadata tags.</param>
    /// <param name="fixFileCorruption">-FX, Attempt fixing file corruption by using FFMpeg for from/target/scan files.</param>
    /// <param name="acoustidApiKey">-AI, When AcoustId API Key is set, try getting the artist/album/title when needed.</param>
    /// <param name="fileFormat">-FF, rename file format {Artist} {SortArtist} {Title} {Album} {Track} {TrackCount} {AlbumArtist} {AcoustId} {AcoustIdFingerPrint} {BitRate}.</param>
    /// <param name="directorySeperator">-ds, Directory Seperator replacer, replace '/' '\' to .e.g. '_'.</param>
    /// <param name="alwaysCheckAcoustId">-ac, Always check & Write to media with AcoustId for missing tags.</param>
    /// <param name="continueScanError">-CS, Continue on scan errors from the Music Libraries.</param>
    /// <param name="overwriteArtist">-OA, Overwrite the Artist name when tagging from MusicBrainz.</param>
    /// <param name="overwriteAlbumArtist">-Oa, Overwrite the Album Artist name when tagging from MusicBrainz.</param>
    /// <param name="overwriteAlbum">-OB, Overwrite the Album name when tagging from MusicBrainz.</param>
    /// <param name="overwriteTrack">-OT, Overwrite the Track name when tagging from MusicBrainz.</param>
    /// <param name="onlyMoveWhenTagged">-MT, Only process/move the media after it was MusicBrainz tagged (-AI must be used) .</param>
    /// <param name="onlyFileNameMatching">-MF, Only filename matching when trying to find duplicates.</param>
    /// <param name="searchByTagNames">-ST, Search MusicBrainz from media tag-values if AcoustId matching failed.</param>
    /// <param name="tidalClientId">-TC, The Client Id used for Tidal's API.</param>
    /// <param name="tidalClientSecret">-TS, The Client Client used for Tidal's API.</param>
    /// <param name="tidalCountryCode">-Tc, Tidal's CountryCode (e.g. US, FR, NL, DE etc).</param>
    [Command("")]
    public static void Root(string from, 
        string target, 
        bool createArtistDirectory, 
        bool createAlbumDirectory, 
        bool parallel,
        bool deleteDuplicateFrom,
        bool deleteDuplicateTo,
        bool dryrun = false,
        bool variousArtists = false,
        bool extraDirMustExist = false,
        bool updateArtistTags = false,
        bool fixFileCorruption = false,
        int skipDirectories = 0,
        List<string>? extrascans = null,
        List<string>? artistDirsMustNotExist = null,
        string? extrascan = null,
        string? acoustidApiKey = "",
        string fileFormat = "",
        string directorySeperator = "_",
        bool alwaysCheckAcoustId = false,
        bool continueScanError = false,
        bool overwriteArtist = false,
        bool overwriteAlbumArtist = false,
        bool overwriteAlbum = false,
        bool overwriteTrack = false,
        bool onlyMoveWhenTagged = false,
        bool onlyFileNameMatching = false,
        bool searchByTagNames = false,
        string tidalClientId = "",
        string tidalClientSecret = "",
        string tidalCountryCode = "US")
    {
        if (!target.EndsWith('/'))
        {
            target += '/';
        }
        if (!from.EndsWith('/'))
        {
            from += '/';
        }
        
        CliOptions options = new CliOptions();
        options.FromDirectory = from;
        options.ToDirectory = target;
        options.CreateAlbumDirectory = createAlbumDirectory;
        options.CreateArtistDirectory = createArtistDirectory;
        options.Parallel = parallel;
        options.SkipFromDirAmount = skipDirectories;
        options.DeleteDuplicateFrom = deleteDuplicateFrom;
        options.DeleteDuplicateTo = deleteDuplicateTo;
        options.RenameVariousArtists = variousArtists;
        options.ExtraDirMustExist = extraDirMustExist;
        options.UpdateArtistTags = updateArtistTags;
        options.FixFileCorruption = fixFileCorruption;
        options.AcoustIdAPIKey = acoustidApiKey;
        options.FileFormat = fileFormat;
        options.DirectorySeperator = directorySeperator;
        options.AlwaysCheckAcoustId = alwaysCheckAcoustId;
        options.ContinueScanError = continueScanError;
        options.OverwriteArtist = overwriteArtist;
        options.OverwriteAlbumArtist = overwriteAlbumArtist;
        options.OverwriteAlbum = overwriteAlbum;
        options.OverwriteTrack = overwriteTrack;
        options.OnlyMoveWhenTagged = onlyMoveWhenTagged;
        options.OnlyFileNameMatching = onlyFileNameMatching;
        options.SearchByTagNames = searchByTagNames;
        options.TidalClientId = tidalClientId;
        options.TidalClientSecret = tidalClientSecret;
        options.TidalCountryCode = tidalCountryCode;
        
        Console.WriteLine("Options used:");
        Console.WriteLine($"From Directory: {options.FromDirectory}");
        Console.WriteLine($"ToDirectory: {options.ToDirectory}");
        Console.WriteLine($"Create Album Directory: {options.CreateAlbumDirectory}");
        Console.WriteLine($"Create Artist Directory: {options.CreateArtistDirectory}");
        Console.WriteLine($"Parallel: {options.Parallel}");
        Console.WriteLine($"Skip From Directory Amount: {options.SkipFromDirAmount}");
        Console.WriteLine($"Delete Duplicate From: {options.DeleteDuplicateFrom}");
        Console.WriteLine($"Delete Duplicate To: {options.DeleteDuplicateTo}");
        Console.WriteLine($"Rename Various Artists: {options.RenameVariousArtists}");
        Console.WriteLine($"Extra Directory Must Exist: {options.ExtraDirMustExist}");
        Console.WriteLine($"Update Artist Tags: {options.UpdateArtistTags}");
        Console.WriteLine($"Fix File Corruption: {options.FixFileCorruption}");
        Console.WriteLine($"AcoustIdAPIKey: {options.AcoustIdAPIKey}");
        Console.WriteLine($"fileFormat: {options.FileFormat}");
        Console.WriteLine($"Always Check AcoustId: {options.AlwaysCheckAcoustId}");
        Console.WriteLine($"Overwrite Artist: {options.OverwriteArtist}");
        Console.WriteLine($"Overwrite Album Artist: {options.OverwriteAlbumArtist}");
        Console.WriteLine($"Overwrite Album: {options.OverwriteAlbum}");
        Console.WriteLine($"Overwrite Track: {options.OverwriteTrack}");
        Console.WriteLine($"Only Move When Tagged: {options.OnlyMoveWhenTagged}");
        Console.WriteLine($"Only FileName Matching: {options.OnlyFileNameMatching}");
        Console.WriteLine($"Search By Tag Names: {options.SearchByTagNames}");

        if (extrascans?.Count > 0)
        {
            foreach (string extraDir in extrascans)
            {
                string extra = extraDir;
                if (!extra.EndsWith('/'))
                {
                    extra += '/';
                }
                options.ExtraScans.Add(extra);
                Console.WriteLine($"Extra scans, {extra}");
            }
        }
        
        if (artistDirsMustNotExist?.Count > 0)
        {
            foreach (string artistDir in artistDirsMustNotExist)
            {
                string directory = artistDir;
                if (!directory.EndsWith('/'))
                {
                    directory += '/';
                }
                options.ArtistDirsMustNotExist.Add(directory);
                Console.WriteLine($"Artist Directories Must Not Exist, {directory}");
            }
        }

        if (!string.IsNullOrWhiteSpace(extrascan))
        {
            if (!extrascan.EndsWith('/'))
            {
                extrascan += '/';
            }
            options.ExtraScans.Add(extrascan);
            Console.WriteLine($"Extra scan, {extrascan}");
        }
        
        MoveProcessor moveProcessor = new MoveProcessor(options);

        if (!string.IsNullOrWhiteSpace(options.FileFormat))
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
        
        moveProcessor.Process();
    }

    private static bool TestFileFormatOutput(MoveProcessor moveProcessor, MediaFileInfo fileInfo, CliOptions options)
    {
        string[] invalidCharacters = new string[] { "?", "<", ">", "=", "{", "}" };
            
        string newFileName = moveProcessor.GetFormatName(fileInfo, options.FileFormat, options.DirectorySeperator);
        if (invalidCharacters.Any(invalidChar => newFileName.Contains(invalidChar)))
        {
            Console.WriteLine($"FileFormat is incorrect, sample output: {newFileName}");
            return false;
        }

        return true;
    }
}