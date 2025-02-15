using ATL;
using MusicMover.Models;

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

    public bool WriteTagFromAcoustId(FileInfo fromFile, string acoustIdAPIKey)
    {
        MusicBrainzAPIService musicBrainzAPIService = new MusicBrainzAPIService();
        AcoustIdService acoustIdService = new AcoustIdService();
        FingerPrintService fingerPrintService = new FingerPrintService();
        FpcalcOutput? fingerprint = fingerPrintService.GetFingerprint(fromFile.FullName);

        if (fingerprint is null)
        {
            return false;
        }
        
        var acoustIdLookup = acoustIdService.LookupAcoustId(acoustIdAPIKey, fingerprint.Fingerprint, (int)fingerprint.Duration);
            
        var recordingId = acoustIdLookup?["results"]?.FirstOrDefault()?["recordings"]?.FirstOrDefault()?["id"]?.ToString();
        var acoustId = acoustIdLookup?["results"]?.FirstOrDefault()?["id"]?.ToString();

        if (string.IsNullOrWhiteSpace(recordingId) || string.IsNullOrWhiteSpace(acoustId))
        {
            return false;
        }
        
        var data = musicBrainzAPIService.GetRecordingById(recordingId);
        MusicBrainzArtistReleaseModel? release = data?.Releases?.FirstOrDefault();
        
        if (release == null)
        {
            return false;
        }
        
        bool trackInfoUpdated = false;
        Track track = new Track(fromFile.FullName);
        
        string? musicBrainzTrackId = release.Media?.FirstOrDefault()?.Tracks?.FirstOrDefault()?.Id;
        string? musicBrainzReleaseArtistId = data?.ArtistCredit?.FirstOrDefault()?.Artist?.Id;
        string? musicBrainzAlbumId = release.Id;
        string? musicBrainzReleaseGroupId = release.ReleaseGroup.Id;
        
        string artists = string.Join(';', data?.ArtistCredit.Select(artist => artist.Name));
        string musicBrainzArtistIds = string.Join(';', data?.ArtistCredit.Select(artist => artist.Artist.Id));
        string isrcs = data?.ISRCS != null ? string.Join(';', data?.ISRCS) : string.Empty;

        MusicBrainzArtistReleaseModel withLabeLInfo = musicBrainzAPIService.GetReleaseWithLabel(release.Id);
        var label = withLabeLInfo?.LabeLInfo?.FirstOrDefault(label => label?.Label?.Type?.ToLower().Contains("production") == true);

        if (label == null && withLabeLInfo?.LabeLInfo?.Count == 1)
        {
            label = withLabeLInfo?.LabeLInfo?.FirstOrDefault();
        }

        if (!string.IsNullOrWhiteSpace(label?.Label?.Name))
        {
            UpdateTag(track, "LABEL", label?.Label.Name, ref trackInfoUpdated);
            UpdateTag(track, "CATALOGNUMBER", label?.CataLogNumber, ref trackInfoUpdated);
        }

        if ((!track.Date.HasValue ||
             track.Date.Value.ToString("yyyy-MM-dd") != release.Date))
        {
            UpdateTag(track, "date", release.Date, ref trackInfoUpdated);
            UpdateTag(track, "originaldate", release.Date, ref trackInfoUpdated);
        }
        else if (release.Date.Length == 4 &&
                 (!track.Date.HasValue ||
                  track.Date.Value.Year.ToString() != release.Date))
        {
            UpdateTag(track, "date", release.Date, ref trackInfoUpdated);
            UpdateTag(track, "originaldate", release.Date, ref trackInfoUpdated);
        }

        if (string.IsNullOrWhiteSpace(track.Title))
        {
            UpdateTag(track, "Title", release.Media?.FirstOrDefault()?.Title, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.Album))
        {
            UpdateTag(track, "Album", release.Title, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.AlbumArtist)  || track.AlbumArtist.ToLower().Contains("various"))
        {
            UpdateTag(track, "AlbumArtist", data.ArtistCredit.FirstOrDefault()?.Name, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.Artist) || track.Artist.ToLower().Contains("various"))
        {
            UpdateTag(track, "Artist", data.ArtistCredit.FirstOrDefault()?.Name, ref trackInfoUpdated);
        }

        UpdateTag(track, "ARTISTS", artists, ref trackInfoUpdated);
        UpdateTag(track, "ISRC", isrcs, ref trackInfoUpdated);
        UpdateTag(track, "SCRIPT", release?.TextRepresentation?.Script, ref trackInfoUpdated);
        UpdateTag(track, "barcode", release.Barcode, ref trackInfoUpdated);

        UpdateTag(track, "MusicBrainz Artist Id", musicBrainzArtistIds, ref trackInfoUpdated);
        UpdateTag(track, "MusicBrainz Track Id", recordingId, ref trackInfoUpdated);
        UpdateTag(track, "MusicBrainz Release Track Id", musicBrainzTrackId, ref trackInfoUpdated);
        UpdateTag(track, "MusicBrainz Release Artist Id", musicBrainzReleaseArtistId, ref trackInfoUpdated);
        UpdateTag(track, "MusicBrainz Release Group Id", musicBrainzReleaseGroupId, ref trackInfoUpdated);
        UpdateTag(track, "MusicBrainz Release Id", release.Id, ref trackInfoUpdated);

        UpdateTag(track, "MusicBrainz Album Artist Id", musicBrainzArtistIds, ref trackInfoUpdated);
        UpdateTag(track, "MusicBrainz Album Id", musicBrainzAlbumId, ref trackInfoUpdated);
        UpdateTag(track, "MusicBrainz Album Type", release.ReleaseGroup.PrimaryType, ref trackInfoUpdated);
        UpdateTag(track, "MusicBrainz Album Release Country", release.Country, ref trackInfoUpdated);
        UpdateTag(track, "MusicBrainz Album Status", release.Status, ref trackInfoUpdated);

        UpdateTag(track, "Acoustid Id", acoustId, ref trackInfoUpdated);

        UpdateTag(track, "Date", release.ReleaseGroup.FirstReleaseDate, ref trackInfoUpdated);
        UpdateTag(track, "originaldate", release.ReleaseGroup.FirstReleaseDate, ref trackInfoUpdated);
        
        if (release.ReleaseGroup.FirstReleaseDate.Length >= 4)
        {
            UpdateTag(track, "originalyear", release.ReleaseGroup.FirstReleaseDate.Substring(0, 4), ref trackInfoUpdated);
        }
        
        UpdateTag(track, "Disc Number", release.Media?.FirstOrDefault()?.Position?.ToString(), ref trackInfoUpdated);
        UpdateTag(track, "Track Number", release.Media?.FirstOrDefault()?.Tracks?.FirstOrDefault()?.Position?.ToString(), ref trackInfoUpdated);
        UpdateTag(track, "Total Tracks", release.Media?.FirstOrDefault()?.TrackCount.ToString(), ref trackInfoUpdated);
        UpdateTag(track, "MEDIA", release.Media?.FirstOrDefault()?.Format, ref trackInfoUpdated);

        return SafeSave(track);
    }
    
    private void UpdateTag(Track track, string tagName, string? value, ref bool trackInfoUpdated)
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
        
        bool tempIsUpdated = false;
        UpdateTrackTag(track, tagName, value, ref tempIsUpdated);

        if (tempIsUpdated)
        {
            Console.WriteLine($"Updating tag '{tagName}' => '{value}'");
            trackInfoUpdated = true;
        }
    }
}