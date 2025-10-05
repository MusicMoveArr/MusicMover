namespace MusicMover.MediaHandlers;

public class MediaHandlerDummy : MediaHandler
{
    public override string? Artist => GetMediaTagValue(nameof(Artist));
    public override string? SortArtist => GetMediaTagValue(nameof(SortArtist));
    public override string? Title => GetMediaTagValue(nameof(Title));
    public override string? Album => GetMediaTagValue(nameof(Album));
    public override int? TrackNumber => GetMediaTagInt(nameof(TrackNumber));
    public override int? TrackCount => GetMediaTagInt(nameof(TrackCount));
    public override string? AlbumArtist => GetMediaTagValue(nameof(AlbumArtist));
    public override string? AcoustId => GetMediaTagValue(nameof(AcoustId));
    public override string? AcoustIdFingerPrint => GetMediaTagValue(nameof(AcoustIdFingerPrint));
    public override float? AcoustIdFingerPrintDuration => 0;
    public override double BitRate => 0;
    public override int? DiscNumber => GetMediaTagInt(nameof(DiscNumber));
    public override int? DiscTotal => GetMediaTagInt(nameof(DiscTotal));
    public override int? TrackTotal => GetMediaTagInt(nameof(TrackTotal));
    public override int Duration => GetMediaTagInt(nameof(Duration)) ?? 0;
    public override int? Year => GetMediaTagInt(nameof(Year));
    public override DateTime? Date => DateTime.Now;
    public override string? CatalogNumber => GetMediaTagValue(nameof(CatalogNumber));
    public override string ISRC => GetMediaTagValue(nameof(ISRC));
    public override bool SaveTo(FileInfo targetFile)
    {
        throw new NotImplementedException();
    }

    protected override void MapMediaTag(string key, string value)
    {
        throw new NotImplementedException();
    }

    public override string GetSetterTagName(string tagName)
    {
        return tagName;
    }

    public void SetArtists()
    {
        base.AllArtistNames.Clear();
        if (!string.IsNullOrWhiteSpace(Artist))
        {
            base.AllArtistNames.Add(Artist);
        }
        if (!string.IsNullOrWhiteSpace(SortArtist))
        {
            base.AllArtistNames.Add(SortArtist);
        }
        if (!string.IsNullOrWhiteSpace(AlbumArtist))
        {
            base.AllArtistNames.Add(AlbumArtist);
        }
    }
}