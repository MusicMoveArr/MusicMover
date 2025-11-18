namespace MusicMover;

public class CliOptions
{
    public bool IsDryRun { get; init; }
    public required string FromDirectory { get; init; }
    public required string FromFile { get; init; }
    public required string ToDirectory { get; init; }
    public required bool CreateArtistDirectory { get; init; }
    public required bool CreateAlbumDirectory { get; init; }
    public required bool Parallel { get; init; }
    public required int SkipFromDirAmount { get; init; }
    public required bool DeleteDuplicateFrom { get; init; }
    public required bool DeleteDuplicateTo { get; init; }
    public List<string> ExtraScans { get; set; } = [];
    public List<string> ArtistDirsMustNotExist { get; set; } = [];
    
    public required bool RenameVariousArtists { get; init; }
    public required bool ExtraDirMustExist { get; init; }
    public required bool UpdateArtistTags { get; init; }
    public required bool FixFileCorruption { get; init; }
    public required string? AcoustIdApiKey { get; init; }
    public required string FileFormat { get; init; }
    public required string DirectorySeperator { get; init; }
    public required bool AlwaysCheckAcoustId { get; init; }
    public required bool ContinueScanError { get; init; }
    public required bool OverwriteArtist { get; init; }
    public required bool OverwriteAlbumArtist { get; init; }
    public required bool OverwriteAlbum { get; init; }
    public required bool OverwriteTrack { get; init; }
    public required bool OnlyMoveWhenTagged { get; init; }
    public required bool OnlyFileNameMatching { get; init; }
    public required bool SearchByTagNames { get; init; }
    public required string TidalClientId { get; init; }
    public required string TidalClientSecret { get; init; }
    public required string TidalCountryCode { get; init; }
    public required string MetadataApiBaseUrl { get; init; }
    public required List<string> MetadataApiProviders { get; init; }
    public required List<string> PreferredFileExtensions { get; init; }
    public required List<string> NonPreferredFileExtensions { get; init; }
    public required bool DebugInfo { get; init; }
    public required int MetadataApiMatchPercentage { get; init; }
    public required int TidalMatchPercentage { get; init; }
    public required int MusicBrainzMatchPercentage { get; init; }
    public required int AcoustIdMatchPercentage { get; init; }
    public required bool TrustAcoustIdWhenTaggingFailed { get; init; }
    public required string MoveUntaggableFilesPath { get; init; }
    public required string MetadataHandlerLibrary { get; init; }
    public required string TranslationPath { get; init; }
    public required string DumpCoverFilename { get; init; }
}