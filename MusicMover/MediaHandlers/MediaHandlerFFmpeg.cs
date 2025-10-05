using FFMpegCore;
using MusicMover.Helpers;

namespace MusicMover.MediaHandlers;

public class MediaHandlerFFmpeg : MediaHandler
{
    private readonly IMediaAnalysis _mediaAnalysis;
    private readonly AudioStream _audioStream;
    
    public override string? Artist => GetMediaTagValue("artist");
    public override string? SortArtist => GetMediaTagValue("artistsort", "artist-sort", "sort_artist", "artistsortorder", "sortartist");
    public override string? Title => GetMediaTagValue("title");
    public override string? Album => GetMediaTagValue("album");
    public override int? TrackNumber => GetMediaTagInt("track");
    public override int? TrackCount => GetMediaTagInt("tracktotal", "total tracks");
    public override string? AlbumArtist => GetMediaTagValue("album_artist", "albumartist");
    public override string? AcoustId => GetMediaTagValue(AcoustIdIdTag, AcoustIdTag);
    public override string? AcoustIdFingerPrint => GetMediaTagValue(AcoustidFingerprintTag);
    public override float? AcoustIdFingerPrintDuration => GetMediaTagFloat(AcoustidFingerprintDurationTag);
    public override double BitRate => _audioStream?.BitRate ?? 0;
    public override int? DiscNumber => GetMediaTagInt("disc", "discnumber", "disc number");
    public override int? DiscTotal => GetMediaTagInt("disctotal", "totaldisc");
    public override int? TrackTotal => GetMediaTagInt("tracktotal", "totaltracks");
    public override int Duration => (int)_audioStream.Duration.TotalSeconds;
    public override int? Year => GetMediaTagInt("originalyear", "year", "date");
    public override DateTime? Date => GetMediaTagDateTime("date", "originaldate");
    public override string? CatalogNumber => GetMediaTagValue("CATALOGNUMBER");
    public override string ISRC => GetMediaTagValue("ISRC");

    public MediaHandlerFFmpeg(FileInfo fileInfo)
        : base(fileInfo)
    {
        _mediaAnalysis = FFProbe.Analyse(fileInfo.FullName);
        _audioStream = _mediaAnalysis.AudioStreams.FirstOrDefault();
        var audioStreamTags = _audioStream.Tags.ToDictionary(StringComparer.OrdinalIgnoreCase);
        var formatTags = _mediaAnalysis.Format.Tags.ToDictionary(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in audioStreamTags)
            base.MediaTags[pair.Key] = pair.Value;

        foreach (var pair in formatTags)
            base.MediaTags[pair.Key] = pair.Value;
        
        AllArtistNames.Clear();
        AllArtistNames.Add(Artist);
        AllArtistNames.Add(AlbumArtist);
        AllArtistNames.Add(ArtistHelper.GetUncoupledArtistName(Artist));
        AllArtistNames.Add(ArtistHelper.GetUncoupledArtistName(AlbumArtist));
        
        AllArtistNames.AddRange(Artist?.Split(new char[] {',', ';'}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? []);
        AllArtistNames.AddRange(AlbumArtist?.Split(new char[] {',', ';'}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? []);
        AllArtistNames = AllArtistNames
            .Where(artist => !string.IsNullOrWhiteSpace(artist))
            .DistinctBy(artist => artist)
            .ToList();
    }
    
    public override bool SaveTo(FileInfo targetFile)
    {
        foreach (var keyValue in base.MediaTags)
        {
            MapMediaTag(keyValue.Key, keyValue.Value);
        }
        
        bool success = FFMpegArguments
            .FromFileInput(FileInfo.FullName)
            .OutputToFile(targetFile.FullName + FileInfo.Extension, overwrite: true, options =>
            {
                options.WithCustomArgument("-codec copy"); 
                options.WithCustomArgument("-map 0");
                    
                foreach (var keyValue in _audioStream.Tags)
                {
                    options = options.WithCustomArgument($"-metadata:s:a:0 \"{keyValue.Key}\"=\"{keyValue.Value}\"");
                }
            })
            .ProcessSynchronously();
        
        //small trick to allow FFmpeg saving to ".bak" file by applying the original file extension
        if (success)
        {
            File.Move(targetFile.FullName + FileInfo.Extension, targetFile.FullName, true);
        }

        return success;
    }

    protected override void MapMediaTag(string key, string value)
    {
        string keyTagName = GetTagName(key);
        _audioStream.Tags[keyTagName] = value;
    }

    public override string GetSetterTagName(string tagName)
    {
        return tagName
            .Replace("-", string.Empty)
            .Replace(" ", "_")
            .Replace("__", "_")
            .Replace("__", "_") //again to be sure
            .ToUpper();
    }
}