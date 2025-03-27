using ATL;

namespace MusicMover.Services;

public class MediaTagWriteService
{
    public bool Save(FileInfo targetFile, string artistName, string albumName, string title)
    {
        bool isUpdated = false;
        Track track = new Track(targetFile.FullName);
        UpdateTrackTag(track, "artist", artistName, ref isUpdated);
        UpdateTrackTag(track, "album", albumName, ref isUpdated);
        UpdateTrackTag(track, "title", title, ref isUpdated);

        return SafeSave(track);
    }
    
    public bool SaveTag(FileInfo targetFile, string tag, string value)
    {
        bool isUpdated = false;
        Track track = new Track(targetFile.FullName);
        UpdateTrackTag(track, tag, value, ref isUpdated);

        return SafeSave(track);
    }
    
    public bool UpdateTrackTag(Track track, string tag, string value, ref bool updated)
    {
        var oldValues = track.AdditionalFields.ToDictionary(StringComparer.OrdinalIgnoreCase);
        switch (tag.ToLower())
        {
            case "title":
                updated = !string.Equals(track.Title, value);
                track.Title = value;
                return true;
            case "album":
                updated = !string.Equals(track.Album, value);
                track.Album = value;
                return true;
            case "albumartist":
                updated = !string.Equals(track.AlbumArtist, value);
                track.AlbumArtist = value;
                return true;
            case "albumartistsortorder":
                updated = !string.Equals(track.SortAlbumArtist, value);
                track.SortAlbumArtist = value;
                return true;
            case "artist-sort":
            case "sort_artist":
            case "artistsortorder":
                updated = !string.Equals(track.SortArtist, value);
                track.SortArtist = value;
                return true;
            case "artists":
                track.AdditionalFields[GetFieldName(track,"ARTISTS")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "ARTISTS");
                return true;
            case "artist":
                updated = !string.Equals(track.Artist, value);
                track.Artist = value;
                return true;
            case "date":
                if (DateTime.TryParse(value, out var result))
                {
                    track.AdditionalFields[GetFieldName(track,"date")] = value;
                    updated = IsDictionaryUpdated(track, oldValues, "date");
                    return true;
                }
                else if (int.TryParse(value, out var result2))
                {
                    track.AdditionalFields[GetFieldName(track,"date")] = value;
                    updated = IsDictionaryUpdated(track, oldValues, "date");
                    return true;
                }
                return false;
            case "catalognumber":
                updated = !string.Equals(track.CatalogNumber, value);
                track.CatalogNumber = value;
                return true;
            case "asin":
                track.AdditionalFields[GetFieldName(track,"asin")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "asin");
                return true;
            case "year":
                if (!int.TryParse(value, out int year))
                {
                    return false;
                }

                updated = track.Year != year;
                track.Year = year;
                return true;
            case "originalyear":
                if (!int.TryParse(value, out int originalyear))
                {
                    return false;
                }
                track.AdditionalFields[GetFieldName(track,"originalyear")] = originalyear.ToString();
                updated = IsDictionaryUpdated(track, oldValues, "originalyear");
                return true;
            case "originaldate":
                track.AdditionalFields[GetFieldName(track,"originaldate")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "originaldate");
                return true;
            case "disc":
            case "disc number":
                if (!int.TryParse(value, out int disc))
                {
                    return false;
                }
                updated = track.DiscNumber != disc;
                track.DiscNumber = disc;
                return true;
            case "track number":
                if (!int.TryParse(value, out int trackNumber))
                {
                    return false;
                }
                updated = track.TrackNumber != trackNumber;
                track.TrackNumber = trackNumber;
                return true;
            case "total tracks":
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
                if (!int.TryParse(value, out int totalDiscs))
                {
                    return false;
                }

                updated = track.DiscTotal != totalDiscs;
                track.DiscTotal = totalDiscs;
                return true;
            case "musicbrainz artist id":
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Artist Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Artist Id");
                return true;
            case "musicbrainz release group id":
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Release Group Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Release Group Id");
                return true;
            case "musicbrainz release artist id":
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Release Artist Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Release Artist Id");
                return true;
            case "musicbrainz release id":
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Release Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Release Id");
                return true;
            case "musicbrainz release track id":
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Release Track Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Release Track Id");
                return true;
            case "musicbrainz track id":
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Track Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Track Id");
                return true;
            case "musicbrainz album artist id":
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Album Artist Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Album Artist Id");
                return true;
            case "musicbrainz album id":
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Album Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Album Id");
                return true;
            case "musicbrainz album type":
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Album Type")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Album Type");
                return true;
            case "musicbrainz album release country":
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Album Release Country")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Album Release Country");
                return true;
            case "musicbrainz album status":
                track.AdditionalFields[GetFieldName(track,"MusicBrainz Album Status")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MusicBrainz Album Status");
                return true;
            case "script":
                track.AdditionalFields[GetFieldName(track,"SCRIPT")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "SCRIPT");
                return true;
            case "barcode":
                track.AdditionalFields[GetFieldName(track, "BARCODE")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "BARCODE");
                return true;
            case "media":
                track.AdditionalFields[GetFieldName(track, "MEDIA")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "MEDIA");
                return true;
            case "acoustid id":
                track.AdditionalFields[GetFieldName(track, "Acoustid Id")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Acoustid Id");
                return true;
            case "acoustid fingerprint duration":
                track.AdditionalFields[GetFieldName(track, "Acoustid Fingerprint Duration")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Acoustid Fingerprint Duration");
                return true;
            case "acoustid fingerprint":
                track.AdditionalFields[GetFieldName(track, "Acoustid Fingerprint")] = value;
                updated = IsDictionaryUpdated(track, oldValues, "Acoustid Fingerprint");
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
    
    public bool SafeSave(Track track)
    {
        FileInfo targetFile = new FileInfo(track.Path);
        string tempFile = $"{track.Path}.tmp{targetFile.Extension}";
        bool success = false;
        try
        {
            success = track.SaveTo(tempFile);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
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

    
}