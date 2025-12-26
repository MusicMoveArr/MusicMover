using MusicMover.MediaHandlers;
using MusicMover.Models;

namespace MusicMover.Rules.Machine;

public class StateObject
{
    public required MediaHandler MediaHandler { get; init; }
    public required CliOptions Options { get; init; }
    
    public DirectoryInfo ToArtistDirInfo { get; set; }
    public DirectoryInfo ToAlbumDirInfo { get; set; }
    
    public bool MetadataApiTaggingSuccess { get; set; }
    public bool MusicBrainzTaggingSuccess { get; set; }
    public bool TidalTaggingSuccess { get; set; }
    public SimilarFileResult SimilarFileResult { get; set; }
}