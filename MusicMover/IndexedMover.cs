using System.Diagnostics;
namespace MusicMover;

//experiment of mine, it's slow at the start but "faster"(?) in general
//Just leaving this unused here till later

public class IndexedMover
{
    /*public static void MoveIndexed(string FromRootDirectory, string ToRootDirectory)
    {
        //1. index files from FromRootDirectory for FileInfo / Media Tags
        //2. index files from ToRootDirectory for FileInfo / Media Tags
        //3. parallel job to replace MP3 with FLAC
        //it finds songs to replace by Title, Album, Artist.
        //it doesn't matter if there are typos in folder names
        
        List<FileModel> FromFiles = new List<FileModel>();
        List<FileModel> ToFiles = new List<FileModel>();
    
        var sortedTopDirectories = Directory
            .EnumerateFileSystemEntries(FromRootDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(file => Directory.Exists(file))
            .OrderBy(dir => dir)
            .ToList();

        var sortedTopToDirectories = Directory
            .EnumerateFileSystemEntries(ToRootDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(file => Directory.Exists(file))
            .OrderBy(dir => dir)
            .Skip(4)
            .ToList();
        
        int index = 0;
        
        var fromFiles = sortedTopDirectories
            .AsParallel()
            .SelectMany(fromTopDir =>
            {
                var fromArtistDirInfo = new DirectoryInfo(fromTopDir);
                var flacFromFiles = fromArtistDirInfo.GetFiles("*.flac", SearchOption.AllDirectories);
        
                return flacFromFiles.AsParallel()
                    .Select(flacFromFile =>
                    {
                        try
                        {
                            TagLib.File fileTag = TagLib.File.Create(flacFromFile.FullName);

                            if (string.IsNullOrWhiteSpace(fileTag.Tag.Title) ||
                                string.IsNullOrWhiteSpace(fileTag.Tag.Album) ||
                                string.IsNullOrWhiteSpace(fileTag.Tag.FirstAlbumArtist))
                            {
                                Debug.WriteLine($"Skipped file due empty tags {flacFromFile.FullName}");
                                return null; // Skip files with errors
                            }
                            
                            var fileModel = new FileModel
                            {
                                File = flacFromFile,
                                FileTag = fileTag
                            };
                            
                            Debug.WriteLine($"Read {index++} From files");
                            return fileModel;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                            return null; // Skip on exception
                        }
                    })
                    .Where(fileModel => fileModel != null); // Filter out failed attempts
            })
            .ToList();

        FromFiles.AddRange(fromFiles);

        index = 0;
        var toFiles = sortedTopToDirectories
            .AsParallel()
            .SelectMany(toTopDir =>
            {
                var toArtistDirInfo = new DirectoryInfo(toTopDir);
                var toFilesInDir = toArtistDirInfo.GetFiles("*.mp3", SearchOption.AllDirectories);

                return toFilesInDir.AsParallel()
                    .Select(toFile =>
                    {
                        try
                        {
                            TagLib.File fileTag = TagLib.File.Create(toFile.FullName);

                            if (string.IsNullOrWhiteSpace(fileTag.Tag.Title) ||
                                string.IsNullOrWhiteSpace(fileTag.Tag.Album) ||
                                string.IsNullOrWhiteSpace(fileTag.Tag.FirstAlbumArtist))
                            {
                                Debug.WriteLine($"Skipped file due empty tags {toFile.FullName}");
                                return null; // Skip files with errors
                            }
                            
                            var fileModel = new FileModel
                            {
                                File = toFile,
                                FileTag = fileTag
                            };
                            Debug.WriteLine($"Read {index++} To files");
                            return fileModel;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                            return null; // Skip files with errors
                        }
                    })
                    .Where(fileModel => fileModel != null); // Filter out null results
            })
            .ToList();

        ToFiles.AddRange(toFiles);
        
        Stopwatch sw = Stopwatch.StartNew();
        int progress = 0;

        FromFiles
            .AsParallel()
            .ForAll(fromFile =>
            {
                var toFilesFound = ToFiles
                    .Where(toFile => toFile.FileTag.Tag.Title == fromFile.FileTag.Tag.Title)
                    .Where(toFile => toFile.FileTag.Tag.Album == fromFile.FileTag.Tag.Album)
                    .Where(toFile => toFile.FileTag.Tag.FirstAlbumArtist == fromFile.FileTag.Tag.FirstAlbumArtist)
                    .ToList();

                if (toFilesFound.Count == 0)
                {
                    // Uncomment if logging for missing files is needed
                    // Console.WriteLine($"Similar File {fromFile.File.FullName} does not exist");
                }
                else if (toFilesFound.Count == 1)
                {
                    try
                    {
                        FileModel toFile = toFilesFound.First();
                        DirectoryInfo toAlbumDirInfo = toFile.File.Directory;

                        string newPath = $"{toAlbumDirInfo.FullName}/{fromFile.File.Name}";
                        Console.WriteLine($"{fromFile.File.FullName} -> {toFile.File.FullName}");

                        fromFile.File.MoveTo(newPath);
                        toFile.File.Delete();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
                else if (toFilesFound.Count > 1)
                {
                    // Uncomment if logging for multiple matches is needed
                    // Console.WriteLine($"File Multiple possibilities {fromFile.FileTag.Tag.Title} exist");
                }

                progress++;

                lock (sw)
                {
                    if (sw.Elapsed.Seconds > 5)
                    {
                        Console.WriteLine($"Progress: {progress} / {FromFiles.Count},  {Math.Round((double)progress / FromFiles.Count * 100D, 1)}%");
                        sw.Restart();
                    }
                }
            });
    }*/
}