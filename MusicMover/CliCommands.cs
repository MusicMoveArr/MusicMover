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
    /// <param name="extrascans">-A, Scan extra directories, usage, ["a","b"], besides the target directory.</param>
    /// <param name="extrascan">-a, Scan a extra directory, besides the target directory.</param>
    /// <param name="variousArtists">-va, Rename "Various Artists" in the file name with First Performer.</param>
    /// <param name="extraDirMustExist">-AX, Artist folder must already exist in the extra scanned directories.</param>
    /// <param name="artistDirsMustNotExist">-AN, Artist folder must not exist in the extra scanned directories, only meant for --createArtistDirectory, -g.</param>
    /// <param name="updateArtistTags">-UA, Update Artist metadata tags.</param>
    /// <param name="fixFileCorruption">-FX, Attempt fixing file corruption by using FFMpeg for from/target/scan files.</param>
    /// <param name="acoustidAPIKey">-AI When AcoustId API Key is set, try getting the artist/album/title when needed.</param>
    /// <param name="fileFormat">-FF rename file format {Artist} {SortArtist} {Title} {Album} {Track} {TrackCount} {AlbumArtist} {AcoustId} {AcoustIdFingerPrint} {BitRate}.</param>
    /// <param name="directorySeperator">-ds, Directory Seperator replacer, replace '/' '\' to .e.g. '_'.</param>
    /// <param name="alwaysCheckAcoustId">-ac, Always check & Write to media with AcoustId for missing tags.</param>
    /// <param name="continueScanError">-CS, Continue on scan errors from the Music Libraries.</param>
    [Command("")]
    public static void Root(string from, 
        string target, 
        bool createArtistDirectory, 
        bool createAlbumDirectory, 
        bool parallel,
        bool deleteDuplicateFrom,
        bool dryrun = false,
        bool variousArtists = false,
        bool extraDirMustExist = false,
        bool updateArtistTags = false,
        bool fixFileCorruption = false,
        int skipDirectories = 0,
        List<string> extrascans = null,
        List<string> artistDirsMustNotExist = null,
        string extrascan = null,
        string acoustidAPIKey = null,
        string fileFormat = "",
        string directorySeperator = "_",
        bool alwaysCheckAcoustId = false,
        bool continueScanError = false)
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
        options.RenameVariousArtists = variousArtists;
        options.ExtraDirMustExist = extraDirMustExist;
        options.UpdateArtistTags = updateArtistTags;
        options.FixFileCorruption = fixFileCorruption;
        options.AcoustIdAPIKey = acoustidAPIKey;
        options.FileFormat = fileFormat;
        options.DirectorySeperator = directorySeperator;
        options.AlwaysCheckAcoustId = alwaysCheckAcoustId;
        options.ContinueScanError = continueScanError;
        
        Console.WriteLine("Options used:");
        Console.WriteLine($"From Directory: {options.FromDirectory}");
        Console.WriteLine($"ToDirectory: {options.ToDirectory}");
        Console.WriteLine($"Create Album Directory: {options.CreateAlbumDirectory}");
        Console.WriteLine($"Create Artist Directory: {options.CreateArtistDirectory}");
        Console.WriteLine($"Parallel: {options.Parallel}");
        Console.WriteLine($"Skip From Directory Amount: {options.SkipFromDirAmount}");
        Console.WriteLine($"Delete Duplicate From: {options.DeleteDuplicateFrom}");
        Console.WriteLine($"Rename Various Artists: {options.RenameVariousArtists}");
        Console.WriteLine($"Extra Directory Must Exist: {options.ExtraDirMustExist}");
        Console.WriteLine($"Update Artist Tags: {options.UpdateArtistTags}");
        Console.WriteLine($"Fix File Corruption: {options.FixFileCorruption}");
        Console.WriteLine($"AcoustIdAPIKey: {options.AcoustIdAPIKey}");
        Console.WriteLine($"fileFormat: {options.FileFormat}");
        Console.WriteLine($"Always Check AcoustId: {options.AlwaysCheckAcoustId}");

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