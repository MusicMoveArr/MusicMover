using ATL;
using ATL.AudioData;
using MusicMover.Helpers;
using Spectre.Console;

namespace MusicMover.Services;

public class MediaTagWriteService
{
    public async Task<bool> SaveAsync(FileInfo targetFile, string artistName, string albumName, string title)
    {
        string orgValue = string.Empty;
        bool isUpdated = false;
        Track track = new Track(targetFile.FullName);
        UpdateTrackTag(track, "artist", artistName, ref isUpdated, ref orgValue);
        UpdateTrackTag(track, "album", albumName, ref isUpdated, ref orgValue);
        UpdateTrackTag(track, "title", title, ref isUpdated, ref orgValue);

        return await SafeSaveAsync(track);
    }
    
    public async Task<bool> SaveTagAsync(FileInfo targetFile, string tag, string value)
    {
        string orgValue = string.Empty;
        bool isUpdated = false;
        Track track = new Track(targetFile.FullName);
        UpdateTrackTag(track, tag, value, ref isUpdated, ref orgValue);

        return await SafeSaveAsync(track);
    }
    
    public bool UpdateTrackTag(Track track, string tag, string value, ref bool updated, ref string orgValue)
    {
        value = value.Trim();
        var oldValues = track.AdditionalFields.ToDictionary();
        switch (tag.ToLower())
        {
            case "title":
                orgValue = track.Title;
                updated = !string.Equals(track.Title, value);
                track.Title = value;
                return true;
            case "album":
                orgValue = track.Album;
                updated = !string.Equals(track.Album, value);
                track.Album = value;
                return true;
            case "albumartist":
            case "album_artist":
                orgValue = track.AlbumArtist;
                updated = !string.Equals(track.AlbumArtist, value);
                track.AlbumArtist = value;
                return true;
            case "albumartistsortorder":
            case "sort_album_artist":
            case "sortalbumartist":
                orgValue = track.SortAlbumArtist;
                updated = !string.Equals(track.SortAlbumArtist, value);
                track.SortAlbumArtist = value;
                return true;
            case "albumartistsort":
                orgValue = GetDictionaryValue(track, "ALBUMARTISTSORT");
                track.AdditionalFields[GetFieldName(track,"ALBUMARTISTSORT")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "ALBUMARTISTSORT");
                return true;
            case "artistsort":
            case "artist-sort":
            case "sort_artist":
            case "artistsortorder":
            case "sortartist":
                orgValue = track.SortArtist;
                updated = !string.Equals(track.SortArtist, value);
                track.SortArtist = value;
                return true;
            case "artists":
                orgValue = GetDictionaryValue(track, "ARTISTS");
                track.AdditionalFields[GetFieldName(track,"ARTISTS")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "ARTISTS");
                return true;
            case "artists_sort":
                orgValue = GetDictionaryValue(track, "ARTISTS_SORT");
                track.AdditionalFields[GetFieldName(track,"ARTISTS_SORT")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "ARTISTS_SORT");
                return true;
            case "artist":
                orgValue = track.Artist;
                updated = !string.Equals(track.Artist, value);
                track.Artist = value;
                return true;
            case "date":
                orgValue = GetDictionaryValue(track, "date");
                if (DateTime.TryParse(value, out var result))
                {
                    DateTime? oldDate = track.Date;
                    track.AdditionalFields[GetFieldName(track,"date")] = value;
                    track.Date = result;
                    updated = track.Date != oldDate || IsDictionaryUpdated(track, oldValues, "date");
                    return true;
                }
                else if (int.TryParse(value, out var result2))
                {
                    int? oldYear = track.Year;
                    track.Year = result2;
                    track.AdditionalFields[GetFieldName(track,"date")] = value;
                    updated = track.Year != oldYear || IsDictionaryUpdated(track, oldValues, "date");
                    return true;
                }
                return false;
            case "catalognumber":
                if (!string.Equals(value, "[None]", StringComparison.OrdinalIgnoreCase))
                {
                    orgValue = track.CatalogNumber;
                    updated = !string.Equals(track.CatalogNumber, value);
                    track.CatalogNumber = value;
                }
                return true;
            case "asin":
                orgValue = GetDictionaryValue(track, "asin");
                track.AdditionalFields[GetFieldName(track,"asin")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "asin");
                return true;
            case "year":
                orgValue = track.Year?.ToString() ?? string.Empty;
                if (!int.TryParse(value, out int year))
                {
                    return false;
                }

                updated = track.Year != year;
                track.Year = year;
                return true;
            case "originalyear":
                orgValue = GetDictionaryValue(track, "originalyear");
                if (!int.TryParse(value, out int originalyear))
                {
                    return false;
                }
                track.AdditionalFields[GetFieldName(track,"originalyear")] = originalyear.ToString();
                updated = IsDictionaryUpdated(track, oldValues, "originalyear");
                return true;
            case "originaldate":
                orgValue = GetDictionaryValue(track, "originaldate");
                track.AdditionalFields[GetFieldName(track,"originaldate")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "originaldate");
                return true;
            case "disc":
            case "disc number":
                orgValue = track.DiscNumber?.ToString() ?? string.Empty;
                if (!int.TryParse(value, out int disc))
                {
                    return false;
                }
                updated = track.DiscNumber != disc;
                track.DiscNumber = disc;
                return true;
            case "track number":
                orgValue = track.TrackNumber?.ToString() ?? string.Empty;
                if (!int.TryParse(value, out int trackNumber))
                {
                    return false;
                }
                updated = track.TrackNumber != trackNumber;
                track.TrackNumber = trackNumber;
                return true;
            case "total tracks":
                orgValue = track.TrackTotal?.ToString() ?? string.Empty;
                if (!int.TryParse(value, out int totalTracks))
                {
                    return false;
                }

                updated = track.TrackTotal != totalTracks;
                track.TrackTotal = totalTracks;
                return true;
            case "totaldiscs":
            case "total discs":
            case "disctotal":
                orgValue = track.DiscTotal?.ToString() ?? string.Empty;
                if (!int.TryParse(value, out int totalDiscs))
                {
                    return false;
                }

                updated = track.DiscTotal != totalDiscs;
                track.DiscTotal = totalDiscs;
                return true;
            case "musicbrainz artist id":
                orgValue = GetDictionaryValue(track, "MusicBrainz Artist Id");
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Artist Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Artist Id");
                return true;
            case "musicbrainz release group id":
                orgValue = GetDictionaryValue(track, "MusicBrainz Release Group Id");
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Release Group Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Release Group Id");
                return true;
            case "musicbrainz release artist id":
                orgValue = GetDictionaryValue(track, "MusicBrainz Release Artist Id");
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Release Artist Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Release Artist Id");
                return true;
            case "musicbrainz release id":
                orgValue = GetDictionaryValue(track, "MusicBrainz Release Id");
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Release Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Release Id");
                return true;
            case "musicbrainz release track id":
                orgValue = GetDictionaryValue(track, "MusicBrainz Release Track Id");
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Release Track Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Release Track Id");
                return true;
            case "musicbrainz track id":
                orgValue = GetDictionaryValue(track, "MusicBrainz Track Id");
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Track Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Track Id");
                return true;
            case "musicbrainz album artist id":
                orgValue = GetDictionaryValue(track, "MusicBrainz Album Artist Id");
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Album Artist Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Album Artist Id");
                return true;
            case "musicbrainz album id":
                orgValue = GetDictionaryValue(track, "MusicBrainz Album Id");
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Album Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Album Id");
                return true;
            case "musicbrainz album type":
                orgValue = GetDictionaryValue(track, "MusicBrainz Album Type");
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Album Type")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Album Type");
                return true;
            case "musicbrainz album release country":
                orgValue = GetDictionaryValue(track, "MusicBrainz Album Release Country");
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Album Release Country")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Album Release Country");
                return true;
            case "musicbrainz album status":
                orgValue = GetDictionaryValue(track, "MusicBrainz Album Status");
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Album Status")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Album Status");
                return true;
            case "script":
                orgValue = GetDictionaryValue(track, "SCRIPT");
                track.AdditionalFields[GetFieldName(track,"SCRIPT")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "SCRIPT");
                return true;
            case "barcode":
                orgValue = GetDictionaryValue(track, "BARCODE");
                track.AdditionalFields[GetFieldName(track, "BARCODE")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "BARCODE");
                return true;
            case "media":
                orgValue = GetDictionaryValue(track, "MEDIA");
                track.AdditionalFields[GetFieldName(track, "MEDIA")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MEDIA");
                return true;
            case "acoustid id":
                orgValue = GetDictionaryValue(track, "Acoustid Id");
                track.AdditionalFields[GetFieldName(track, "Acoustid Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Acoustid Id");
                return true;
            case "acoustid fingerprint":
                orgValue = GetDictionaryValue(track, "Acoustid Fingerprint");
                track.AdditionalFields[GetFieldName(track, "Acoustid Fingerprint")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Acoustid Fingerprint");
                return true;
            case "acoustid fingerprint duration":
                orgValue = GetDictionaryValue(track, "Acoustid Fingerprint Duration");
                track.AdditionalFields[GetFieldName(track, "Acoustid Fingerprint Duration")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Acoustid Fingerprint Duration");
                return true;
            case "isrc":
                orgValue = track.ISRC;
                updated = !string.Equals(track.ISRC, value);
                track.ISRC = value;
                return true;
            case "label":
                if (!string.Equals(value, "[no label]", StringComparison.OrdinalIgnoreCase))
                {
                    orgValue = GetDictionaryValue(track, "LABEL");
                    track.AdditionalFields[GetFieldName(track, "LABEL")] = value;
                    updated = IsDictionaryUpdated(track, oldValues, "LABEL");
                }
                return true;
            case "spotify track id":
                orgValue = GetDictionaryValue(track, "Spotify Track Id");
                track.AdditionalFields[GetFieldName(track, "Spotify Track Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Spotify Track Id");
                return true;
            case "spotify track explicit":
                orgValue = GetDictionaryValue(track, "Spotify Track Explicit");
                track.AdditionalFields[GetFieldName(track, "Spotify Track Explicit")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Spotify Track Explicit");
                return true;
            case "spotify track uri":
                orgValue = GetDictionaryValue(track, "Spotify Track Uri");
                track.AdditionalFields[GetFieldName(track, "Spotify Track Uri")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Spotify Track Uri");
                return true;
            case "spotify track href":
                orgValue = GetDictionaryValue(track, "Spotify Track Href");
                track.AdditionalFields[GetFieldName(track, "Spotify Track Href")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Spotify Track Href");
                return true;
            case "spotify album id":
                orgValue = GetDictionaryValue(track, "Spotify Album Id");
                track.AdditionalFields[GetFieldName(track, "Spotify Album Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Spotify Album Id");
                return true;
            case "spotify album group":
                orgValue = GetDictionaryValue(track, "Spotify Album Group");
                track.AdditionalFields[GetFieldName(track, "Spotify Album Group")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Spotify Album Group");
                return true;
            case "spotify album release date":
                orgValue = GetDictionaryValue(track, "Spotify Album Release Date");
                track.AdditionalFields[GetFieldName(track, "Spotify Album Release Date")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Spotify Album Release Date");
                return true;
            case "spotify artist href":
                orgValue = GetDictionaryValue(track, "Spotify Artist Href");
                track.AdditionalFields[GetFieldName(track, "Spotify Artist Href")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Spotify Artist Href");
                return true;
            case "spotify artist genres":
                orgValue = GetDictionaryValue(track, "Spotify Artist Genres");
                track.AdditionalFields[GetFieldName(track, "Spotify Artist Genres")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Spotify Artist Genres");
                return true;
            case "spotify artist id":
                orgValue = GetDictionaryValue(track, "Spotify Artist Id");
                track.AdditionalFields[GetFieldName(track, "Spotify Artist Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Spotify Artist Id");
                return true;
            case "upc":
                orgValue = GetDictionaryValue(track, "UPC");
                track.AdditionalFields[GetFieldName(track, "UPC")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "UPC");
                return true;
            case "genre":
                orgValue = GetDictionaryValue(track, "genre");
                track.AdditionalFields[GetFieldName(track, "genre")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "genre");
                return true;
            case "tidal track id":
                orgValue = GetDictionaryValue(track, "Tidal Track Id");
                track.AdditionalFields[GetFieldName(track, "Tidal Track Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Tidal Track Id");
                return true;
            case "tidal track explicit":
                orgValue = GetDictionaryValue(track, "Tidal Track Explicit");
                track.AdditionalFields[GetFieldName(track, "Tidal Track Explicit")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Tidal Track Explicit");
                return true;
            case "tidal track href":
                orgValue = GetDictionaryValue(track, "Tidal Track Href");
                track.AdditionalFields[GetFieldName(track, "Tidal Track Href")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Tidal Track Href");
                return true;
            case "tidal album id":
                orgValue = GetDictionaryValue(track, "Tidal Album Id");
                track.AdditionalFields[GetFieldName(track, "Tidal Album Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Tidal Album Id");
                return true;
            case "tidal album href":
                orgValue = GetDictionaryValue(track, "Tidal Album Href");
                track.AdditionalFields[GetFieldName(track, "Tidal Album Href")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Tidal Album Href");
                return true;
            case "tidal album release date":
                orgValue = GetDictionaryValue(track, "Tidal Album Release Date");
                track.AdditionalFields[GetFieldName(track, "Tidal Album Release Date")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Tidal Album Release Date");
                return true;
            case "tidal artist id":
                orgValue = GetDictionaryValue(track, "Tidal Artist Id");
                track.AdditionalFields[GetFieldName(track, "Tidal Artist Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Tidal Artist Id");
                return true;
            case "tidal artist href":
                orgValue = GetDictionaryValue(track, "Tidal Artist Href");
                track.AdditionalFields[GetFieldName(track, "Tidal Artist Href")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Tidal Artist Href");
                return true;
            case "copyright":
                orgValue = GetDictionaryValue(track, "Copyright");
                track.AdditionalFields[GetFieldName(track, "Copyright")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Copyright");
                return true;
            case "deezer track id":
                orgValue = GetDictionaryValue(track, "Deezer Track Id");
                track.AdditionalFields[GetFieldName(track, "Deezer Track Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Deezer Track Id");
                return true;
            case "deezer track explicit":
                orgValue = GetDictionaryValue(track, "Deezer Track Explicit");
                track.AdditionalFields[GetFieldName(track, "Deezer Track Explicit")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Deezer Track Explicit");
                return true;
            case "deezer track href":
                orgValue = GetDictionaryValue(track, "Deezer Track Href");
                track.AdditionalFields[GetFieldName(track, "Deezer Track Href")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Deezer Track Href");
                return true;
            case "deezer album id":
                orgValue = GetDictionaryValue(track, "Deezer Album Id");
                track.AdditionalFields[GetFieldName(track, "Deezer Album Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Deezer Album Id");
                return true;
            case "deezer album href":
                orgValue = GetDictionaryValue(track, "Deezer Album Href");
                track.AdditionalFields[GetFieldName(track, "Deezer Album Href")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Deezer Album Href");
                return true;
            case "deezer album release date":
                orgValue = GetDictionaryValue(track, "Deezer Album Release Date");
                track.AdditionalFields[GetFieldName(track, "Deezer Album Release Date")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Deezer Album Release Date");
                return true;
            case "deezer artist id":
                orgValue = GetDictionaryValue(track, "Deezer Artist Id");
                track.AdditionalFields[GetFieldName(track, "Deezer Artist Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Deezer Artist Id");
                return true;
            case "deezer artist href":
                orgValue = GetDictionaryValue(track, "Deezer Artist Href");
                track.AdditionalFields[GetFieldName(track, "Deezer Artist Href")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Deezer Artist Href");
                return true;
        }

        return false;
    }
    
    private bool IsDictionaryUpdated(Track track,  Dictionary<string, string> oldValues, string tagName)
    {
        string fieldName = GetFieldName(track, tagName);

        if (track.AdditionalFields.ContainsKey(fieldName) &&
            !oldValues.ContainsKey(fieldName))
        {
            return true;
        }
        
        return string.Equals(track.AdditionalFields[GetFieldName(track, fieldName)], oldValues[GetFieldName(track, fieldName)]);
    }
    
    private string GetDictionaryValue(Track track, string fieldName)
    {
        fieldName = GetFieldName(track, fieldName);
        if (track.AdditionalFields.TryGetValue(fieldName, out string value))
        {
            return value;
        }
        return string.Empty;
    }
    
    public string GetFieldName(Track track, string field)
    {
        if (track.AdditionalFields.Keys.Any(key => key.ToLower() == field.ToLower()))
        {
            return track.AdditionalFields.First(pair => pair.Key.ToLower() == field.ToLower()).Key;
        }

        return field;
    }

    public string GetTagValue(Track track, string tagName)
    {
        string fieldName = GetFieldName(track, tagName);
        if (track.AdditionalFields.ContainsKey(fieldName))
        {
            return track.AdditionalFields[fieldName];
        }

        return string.Empty;
    }
    
    public async Task<bool> SafeSaveAsync(Track track)
    {
        FileInfo targetFile = new FileInfo(track.Path);
        string tempFile = $"{track.Path}.tmp{targetFile.Extension}";

        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
        
        bool success = false;
        try
        {
            success = await track.SaveToAsync(tempFile);
        }
        catch (Exception ex)
        {
            Logger.WriteLine(ex.Message);
        }

        if (success && File.Exists(tempFile))
        {
            File.Move(tempFile, targetFile.FullName, true);
        }
        else if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }

        return success;
    }

    public void UpdateTag(Track track,
        string tagName, 
        string? value, 
        ref bool trackInfoUpdated)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (int.TryParse(value, out int intValue) && intValue == 0)
        {
            return;
        }
        
        tagName = GetFieldName(track, tagName);
        
        string orgValue = string.Empty;
        bool tempIsUpdated = false;
        UpdateTrackTag(track, tagName, value, ref tempIsUpdated, ref orgValue);
        
        if (tempIsUpdated && !string.Equals(orgValue, value))
        {
            if (value.Length > 100)
            {
                value = value.Substring(0, 100) + "...";
            }
            if (orgValue.Length > 100)
            {
                orgValue = orgValue.Substring(0, 100) + "...";
            }
            
            Console.WriteLine($"Updating tag '{tagName}' value '{orgValue}' =>  '{value}'");
            trackInfoUpdated = true;
        }
    }
    
}