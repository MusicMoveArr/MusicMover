namespace MusicMover;

public class CliOptions
{
    public bool IsDryRun { get; set; }
    public string FromDirectory { get; set; }
    public string ToDirectory { get; set; }
    public bool CreateArtistDirectory { get; set; }
    public bool CreateAlbumDirectory { get; set; }
    public bool Parallel { get; set; }
    public int SkipFromDirAmount { get; set; }
    public bool DeleteDuplicateFrom { get; set; }
    public List<string> ExtraScans { get; set; } = new List<string>();
    public bool RenameVariousArtists { get; set; }
    public bool ExtraDirMustExist { get; set; }
}