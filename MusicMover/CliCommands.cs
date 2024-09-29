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
        int skipDirectories = 0,
        List<string> extrascans = null,
        string extrascan = null)
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
            }
        }

        if (!string.IsNullOrWhiteSpace(extrascan))
        {
            if (!extrascan.EndsWith('/'))
            {
                extrascan += '/';
            }
            options.ExtraScans.Add(extrascan);
        }
        
        
        
        MoveProcessor moveProcessor = new MoveProcessor(options);
        moveProcessor.Process();
    }
}