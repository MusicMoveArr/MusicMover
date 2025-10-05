using System.Globalization;
using MusicMover.Models;
using MusicMover.Services;

namespace MusicMover.MediaHandlers;

public abstract class MediaHandler
{
    public const string AcoustidFingerprintTag = "Acoustid Fingerprint";
    public const string AcoustidFingerprintDurationTag = "Acoustid Fingerprint Duration";
    public const string AcoustIdIdTag = "AcoustidId";
    public const string AcoustIdTag = "AcoustId";
    
    public FileInfo FileInfo { get; set; }
    public FileInfo TargetSaveFileInfo { get; set; }
    private FingerPrintService _fingerPrintService;
    public bool TaggerUpdatedTags { get; set; }

    public abstract string? Artist { get; }
    public abstract string? SortArtist { get; }
    public abstract string? Title { get; }
    public abstract string? Album { get; }
    public abstract int? TrackNumber { get; }
    public abstract int? TrackCount { get; }
    public abstract string? AlbumArtist { get; }
    public abstract string? AcoustId { get; }
    public abstract string? AcoustIdFingerPrint { get; }
    public abstract float? AcoustIdFingerPrintDuration { get; }
    public abstract double BitRate { get; }
    public abstract int? DiscNumber { get; }
    public abstract int? DiscTotal { get; }
    public abstract int? TrackTotal { get; }
    public abstract int Duration { get; }
    public abstract int? Year { get; }
    public abstract DateTime? Date { get; }
    public abstract string? CatalogNumber { get; }
    public abstract string ISRC { get; }
    
    public List<string> AllArtistNames { get; protected set; }
    protected Dictionary<string, string> MediaTags { get; set; }
    
    
    
    //during processing
    public bool MetadataApiTaggingSuccess { get; set; }
    public bool MusicBrainzTaggingSuccess { get; set; }
    public bool TidalTaggingSuccess { get; set; }
    public SimilarFileResult SimilarFileResult { get; set; }
    
    public abstract bool SaveTo(FileInfo targetFile);
    protected abstract void MapMediaTag(string key, string value);

    public MediaHandler()
    {
        this._fingerPrintService = new FingerPrintService();
        this.AllArtistNames = new List<string>();
        this.MediaTags = new Dictionary<string, string>();
    }
    
    public MediaHandler(FileInfo fileInfo)
        : this()
    {
        this.FileInfo = fileInfo;
        this.TargetSaveFileInfo = fileInfo;
    }

    public abstract string GetSetterTagName(string tagName);

    public string GetFirstTagNameWithValue(string[] tagNames)
    {
        string firstKey = GetTagName(tagNames.First());
        
        var keyWithValue = MediaTags.FirstOrDefault(tag => 
            tagNames.Any(tagName => string.Equals(GetSetterTagName(tagName), GetSetterTagName(tag.Key), StringComparison.OrdinalIgnoreCase)));

        return GetSetterTagName(!string.IsNullOrWhiteSpace(keyWithValue.Value) ? keyWithValue.Key : firstKey);
    }
    
    public string GetTagName(string tagName)
    {
        tagName = GetSetterTagName(tagName);
        if (MediaTags.Keys.Any(key => string.Equals(GetSetterTagName(key), tagName, StringComparison.OrdinalIgnoreCase)))
        {
            return GetSetterTagName(MediaTags.First(pair => string.Equals(GetSetterTagName(pair.Key), tagName, StringComparison.OrdinalIgnoreCase)).Key);
        }
        return GetSetterTagName(tagName);
    }
    
    public string GetTagName(Dictionary<string, string> dictionary, string tagName)
    {
        tagName = GetSetterTagName(tagName);
        if (dictionary.Keys.Any(key => string.Equals(GetSetterTagName(key), tagName, StringComparison.OrdinalIgnoreCase)))
        {
            return GetSetterTagName(dictionary.First(pair => string.Equals(GetSetterTagName(pair.Key), tagName, StringComparison.OrdinalIgnoreCase)).Key);
        }
        return GetSetterTagName(tagName);
    }

    public void SetMediaTagValue(string? value, params string[] tagNames)
    {
        string keyTagName = GetFirstTagNameWithValue(tagNames);
        MediaTags[keyTagName] = value ?? string.Empty;
    }

    public void SetMediaTagValue(int? value, params string[] tagNames)
    {
        string keyTagName = GetFirstTagNameWithValue(tagNames);
        MediaTags[keyTagName] = value.ToString() ?? string.Empty;
    }

    public void SetMediaTagValue(float value, params string[] tagNames)
    {
        string keyTagName = GetFirstTagNameWithValue(tagNames);
        MediaTags[keyTagName] = value.ToString() ?? string.Empty;
    }

    public string GetMediaTagValue(params string[] tagNames)
    {
        string keyTagName = GetFirstTagNameWithValue(tagNames);

        string value = MediaTags.FirstOrDefault(pair =>
            string.Equals(GetSetterTagName(pair.Key), keyTagName, StringComparison.OrdinalIgnoreCase)).Value;
        
        return !string.IsNullOrWhiteSpace(value) ? value : string.Empty;
    }

    public int? GetMediaTagInt(params string[] tagNames)
    {
        string? strValue = GetMediaTagValue(tagNames);

        if (int.TryParse(strValue, out int intValue))
        {
            return intValue;
        }

        return null;
    }

    public DateTime? GetMediaTagDateTime(params string[] tagNames)
    {
        string? strValue = GetMediaTagValue(tagNames);

        if (DateTime.TryParse(strValue, out DateTime dateValue))
        {
            return dateValue;
        }

        return null;
    }

    public float? GetMediaTagFloat(params string[] tagNames)
    {
        string? strValue = GetMediaTagValue(tagNames);

        if(float.TryParse(strValue, CultureInfo.InvariantCulture, out float floatValue))
        {
            return floatValue;
        }
        return null;
    }
    
    public async Task<bool> GenerateSaveFingerprintAsync()
    {
        if (!string.IsNullOrWhiteSpace(AcoustIdFingerPrint) &&
            AcoustIdFingerPrintDuration > 0)
        {
            return false;
        }

        FpcalcOutput? fingerprint = await _fingerPrintService.GetFingerprintAsync(FileInfo.FullName);
        if (string.IsNullOrWhiteSpace(fingerprint?.Fingerprint))
        {
            return false;
        }

        MediaTagWriteService mediaTagWriteService = new MediaTagWriteService();

        bool updated = false;
        string originalValue = string.Empty;
        mediaTagWriteService.UpdateTrackTag(this,
            AcoustidFingerprintTag,
            fingerprint?.Fingerprint ?? string.Empty,
            ref updated,
            ref originalValue);
        
        mediaTagWriteService.UpdateTrackTag(this,
            AcoustidFingerprintDurationTag,
            (fingerprint?.Duration ?? 0).ToString(),
            ref updated,
            ref originalValue);
        
        SetMediaTagValue(fingerprint.Fingerprint ?? string.Empty, AcoustidFingerprintTag);
        SetMediaTagValue(fingerprint?.Duration ?? 0, AcoustidFingerprintDurationTag);

        return true;
    }
}