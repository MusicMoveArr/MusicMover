namespace MusicMover;

public class CliOptions
{
    public bool IsDryRun { get; set; }
    public required string FromDirectory { get; init; }
    public required string ToDirectory { get; init; }
    public bool CreateArtistDirectory { get; set; }
    public bool CreateAlbumDirectory { get; set; }
    public bool Parallel { get; set; }
    public int SkipFromDirAmount { get; set; }
    public bool DeleteDuplicateFrom { get; set; }
    public bool DeleteDuplicateTo { get; set; }
    public List<string> ExtraScans { get; set; } = new List<string>();
    public List<string> ArtistDirsMustNotExist { get; set; } = new List<string>();
    
    public bool RenameVariousArtists { get; set; }
    public bool ExtraDirMustExist { get; set; }
    public bool UpdateArtistTags { get; set; }
    public bool FixFileCorruption { get; set; }
    public required string? AcoustIdApiKey { get; init; }
    public required string FileFormat { get; init; }
    public required string DirectorySeperator { get; init; }
    public bool AlwaysCheckAcoustId { get; set; }
    public bool ContinueScanError { get; set; }
    public bool OverwriteArtist { get; set; }
    public bool OverwriteAlbumArtist { get; set; }
    public bool OverwriteAlbum { get; set; }
    public bool OverwriteTrack { get; set; }
    public bool OnlyMoveWhenTagged { get; set; }
    public bool OnlyFileNameMatching { get; set; }
    public bool SearchByTagNames { get; set; }
    public required string TidalClientId { get; init; }
    public required string TidalClientSecret { get; init; }
    public required string TidalCountryCode { get; init; }
    public required string MetadataApiBaseUrl { get; init; }
    public required List<string> MetadataApiProviders { get; init; }
    public required List<string> PreferredFileExtensions { get; init; }
    public required List<string> NonPreferredFileExtensions { get; init; }
    public required bool DebugInfo { get; init; }
    
}