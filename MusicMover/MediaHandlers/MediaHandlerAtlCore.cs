using ATL;
using MusicMover.Helpers;

namespace MusicMover.MediaHandlers;

public class MediaHandlerAtlCore : MediaHandler
{
    public Track TrackInfo { get; private set; }
    private List<string> _ignoreAdditionalFieldTags;

    public MediaHandlerAtlCore(FileInfo fileInfo)
        : base(fileInfo)
    {
        _ignoreAdditionalFieldTags = new List<string>();
        
        //load with a 5 second maximum
        //ATL.Core has sometimes a bug where it will loop infinitely causing 100% cpu usage on some files
        //we need to get out of this 100% cpu usage loop so cancel the token to achieve this
        CancellationTokenSource cancellationToken = new CancellationTokenSource();
        var readRask = Task.Run(() =>
        {
            this.TrackInfo = new Track(fileInfo.FullName);
        }, cancellationToken.Token);
        Task.WhenAny(readRask, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();
        
        if (this.TrackInfo == null)
        {
            try
            {
                cancellationToken.Cancel();
            }
            catch { }
            
            throw new FileLoadException($"It took too long to load '{fileInfo.FullName}'");
        }
        SetTrackInfo(this.TrackInfo);
    }

    public override string? Artist => GetMediaTagValue("artist");
    public override string? SortArtist => GetMediaTagValue("artistsort", "artist-sort", "sort_artist", "artistsortorder", "sortartist");
    public override string? Title => GetMediaTagValue("title");
    public override string? Album => GetMediaTagValue("album");

    public override int? TrackCount => GetMediaTagInt("tracktotal", "total tracks") ?? 0;
    public override string? AlbumArtist => GetMediaTagValue("album_artist", "albumartist");
    public override string? AcoustId => GetMediaTagValue(AcoustIdIdTag, AcoustIdTag);
    public override string? AcoustIdFingerPrint => GetMediaTagValue(AcoustidFingerprintTag);
    public override float? AcoustIdFingerPrintDuration => GetMediaTagFloat(AcoustidFingerprintDurationTag) ?? 0;
    public override double BitRate => TrackInfo.Bitrate;
    public override int Duration => TrackInfo.Duration;
    public override int? Year => TrackInfo.Year;
    public override DateTime? Date => TrackInfo.Date;
    public override string? CatalogNumber => TrackInfo.CatalogNumber;
    public override string ISRC => TrackInfo.ISRC;
    public override int? DiscTotal => GetMediaTagInt("disctotal", "totaldisc") ?? 0;

    public override int? DiscNumber
    {
        get
        {
            string disc = GetMediaTagValue("disc", "discnumber", "disc number");
            int discNumber = 0;
            if (int.TryParse(disc, out discNumber))
            {
                return discNumber;
            }
            if (disc?.Contains('/') == true)
            {
                return int.TryParse(disc.Split('/').Skip(1).FirstOrDefault(), out discNumber) ? discNumber : 0;
            }
            return 0;
        }
    }

    public override int? TrackTotal
    {
        get
        {
            int trackTotal = GetMediaTagInt("tracktotal", "totaltracks") ?? 0;
            string track = GetMediaTagValue("track", "tracknumber");
            if (trackTotal > 0)
            {
                return trackTotal;
            }
            if (track?.Contains('/') == true)
            {
                return int.TryParse(track.Split('/').Skip(1).FirstOrDefault(), out trackTotal) ? trackTotal : 0;
            }
            return 0;
        }
    }
    public override int? TrackNumber
    {
        get
        {
            string track = GetMediaTagValue("track", "tracknumber");
            int trackNumber = 0;
            if (track?.Contains('/') == true)
            {
                return int.TryParse(track.Split('/').FirstOrDefault(), out trackNumber) ? trackNumber : 0;
            }
            return int.TryParse(track, out  trackNumber) ? trackNumber : 0;
        }
    }
    


    public void SetTrackInfo(Track trackInfo)
    {
        this.TrackInfo = trackInfo;
        var mediaTags = TrackInfo.AdditionalFields
            .GroupBy(pair => pair.Key.ToLower())
            .Select(pair => pair.First())
            .ToDictionary(StringComparer.OrdinalIgnoreCase);
        
        //add all non-AdditionalFields
        TrackInfo
            .GetType()
            .GetProperties()
            .ToList()
            .ForEach(prop =>
            {
                object? value = prop.GetValue(TrackInfo);
                
                if (value is not null &&
                    (value is string || (value is int val && val > 0)) &&
                    !mediaTags.ContainsKey(prop.Name))
                {
                    _ignoreAdditionalFieldTags.Add(prop.Name);
                    mediaTags[prop.Name] = value.ToString().Trim();
                }
            });
        
        base.MediaTags.Clear();
        foreach (var tag in mediaTags.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)))
        {
            base.MediaTags.TryAdd(tag.Key, tag.Value);
        }
        
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

        return TrackInfo.SaveTo(targetFile.FullName);
    }

    public override string GetSetterTagName(string tagName)
    {
        return tagName;
    }

    protected override void MapMediaTag(string key, string value)
    {
        switch (key.ToLower())
        {
            case "title":
                TrackInfo.Title = value;
                break;
            case "album":
                TrackInfo.Album = value;
                break;
            case "albumartist":
            case "album_artist":
                TrackInfo.AlbumArtist = value;
                break;
            case "albumartistsortorder":
            case "sort_album_artist":
            case "sortalbumartist":
            case "albumartistsort":
                TrackInfo.SortAlbumArtist = value;
                break;
            case "artistsort":
            case "artist-sort":
            case "sort_artist":
            case "artistsortorder":
            case "sortartist":
                TrackInfo.SortArtist = value;
                break;
            case "artist":
                TrackInfo.Artist = value;
                break;
            case "date":
                if (DateTime.TryParse(value, out var result))
                {
                    TrackInfo.Date = result;
                }
                else if (int.TryParse(value, out var result2))
                {
                    TrackInfo.Year = result2;
                }
                break;
            case "year":
                if (int.TryParse(value, out int year))
                {
                    TrackInfo.Year = year;
                }
                break;
            case "catalognumber":
                if (!string.Equals(value, "[None]", StringComparison.OrdinalIgnoreCase))
                {
                    TrackInfo.CatalogNumber = value;
                }
                break;
            case "disc":
            case "disc number":
                if (int.TryParse(value, out int disc))
                {
                    TrackInfo.DiscNumber = disc;
                }
                break;
            case "totaldiscs":
            case "total discs":
            case "disctotal":
                if (!int.TryParse(value, out int totalDiscs))
                {
                    TrackInfo.DiscTotal = totalDiscs;
                }
                break;
            case "track number":
                if (int.TryParse(value, out int trackNumber))
                {
                    TrackInfo.TrackNumber = trackNumber;
                }
                break;
            case "total tracks":
                if (int.TryParse(value, out int totalTracks))
                {
                    TrackInfo.TrackTotal = totalTracks;
                }
                break;
            case "isrc":
                TrackInfo.ISRC = value;
                break;
            case "label":
                if (!string.Equals(value, "[no label]", StringComparison.OrdinalIgnoreCase))
                {
                    string keyTagName = GetTagName(TrackInfo.AdditionalFields.ToDictionary(), "label");
                    TrackInfo.AdditionalFields[keyTagName] = value;
                }
                break;
            default:
                string defaultKeyTagName = GetTagName(TrackInfo.AdditionalFields.ToDictionary(), key);
                if (!_ignoreAdditionalFieldTags.Contains(defaultKeyTagName))
                {
                    TrackInfo.AdditionalFields[defaultKeyTagName] = value;
                }
                break;
        }
    }
}