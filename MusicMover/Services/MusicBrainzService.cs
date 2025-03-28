using System.Globalization;
using ATL;
using FuzzySharp;
using MusicMover.Models;
using Newtonsoft.Json.Linq;

namespace MusicMover.Services;

public class MusicBrainzService
{
    private readonly MusicBrainzAPIService _musicBrainzAPIService;
    private readonly AcoustIdService _acoustIdService;
    private readonly FingerPrintService _fingerPrintService;
    private readonly MediaTagWriteService _mediaTagWriteService;
        
    public MusicBrainzService()
    {
        _musicBrainzAPIService = new MusicBrainzAPIService();
        _acoustIdService = new AcoustIdService();
        _fingerPrintService = new FingerPrintService();
        _mediaTagWriteService = new MediaTagWriteService();
        
    }
    
    public bool WriteTagFromAcoustId(MediaFileInfo mediaFileInfo, FileInfo fromFile, string acoustIdAPIKey,
        bool overWriteArtist, bool overWriteAlbum, bool overWriteTrack, bool overwriteAlbumArtist)
    {
        FpcalcOutput? fingerprint = _fingerPrintService.GetFingerprint(fromFile.FullName);

        JObject? acoustIdLookup = null;
        string recordingId = string.Empty;
        string acoustId = string.Empty;
        
        if (fingerprint is null)
        {
            Console.WriteLine("Failed to generate fingerprint, corrupt file?");
        }
        
        if (fingerprint is null && !string.IsNullOrWhiteSpace(mediaFileInfo.AcoustId))
        {
            Console.WriteLine($"Looking up AcoustID provided by AcoustId Tag, '{fromFile.FullName}'");
            
            //try again but with the AcoustID from the media file
            acoustIdLookup = _acoustIdService.LookupByAcoustId(acoustIdAPIKey, mediaFileInfo.AcoustId);
            recordingId = acoustIdLookup?["results"]?.FirstOrDefault()?["recordings"]?.FirstOrDefault()?["id"]?.ToString();
            acoustId = acoustIdLookup?["results"]?.FirstOrDefault()?["id"]?.ToString();
            
            if (!string.IsNullOrWhiteSpace(recordingId) && !string.IsNullOrWhiteSpace(acoustId))
            {
                Console.WriteLine($"Found AcoustId info from the AcoustId service by the AcoustID provided by the media Tag, '{fromFile.FullName}'");
            }
        }

        if (fingerprint is null && string.IsNullOrWhiteSpace(recordingId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(recordingId))
        {
            acoustIdLookup = _acoustIdService.LookupAcoustId(acoustIdAPIKey, fingerprint.Fingerprint, (int)fingerprint.Duration);
            recordingId = acoustIdLookup?["results"]?.FirstOrDefault()?["recordings"]?.FirstOrDefault()?["id"]?.ToString();
            acoustId = acoustIdLookup?["results"]?.FirstOrDefault()?["id"]?.ToString();

            if (!string.IsNullOrWhiteSpace(mediaFileInfo.AcoustId) && 
                (string.IsNullOrWhiteSpace(recordingId) || string.IsNullOrWhiteSpace(acoustId)))
            {
                Console.WriteLine($"Looking up AcoustID provided by AcoustId Tag, '{fromFile.FullName}'");
            
                //try again but with the AcoustID from the media file
                acoustIdLookup = _acoustIdService.LookupByAcoustId(acoustIdAPIKey, mediaFileInfo.AcoustId);
                recordingId = acoustIdLookup?["results"]?.FirstOrDefault()?["recordings"]?.FirstOrDefault()?["id"]?.ToString();
                acoustId = acoustIdLookup?["results"]?.FirstOrDefault()?["id"]?.ToString();
            
                if (!string.IsNullOrWhiteSpace(recordingId) && !string.IsNullOrWhiteSpace(acoustId))
                {
                    Console.WriteLine($"Found AcoustId info from the AcoustId service by the AcoustID provided by the media Tag, '{fromFile.FullName}'");
                }
            }
        }
        
        if (string.IsNullOrWhiteSpace(recordingId) || string.IsNullOrWhiteSpace(acoustId))
        {
            return false;
        }
        
        var data = _musicBrainzAPIService.GetRecordingById(recordingId);
        
        //grab the best matched release
        Track track = new Track(fromFile.FullName);

        var bestMatchedArtist =
            !string.IsNullOrWhiteSpace(track.AlbumArtist)
                ? data?.ArtistCredit
                    .Select(artist => new
                    {
                        Artist = artist,
                        MatchedFor = Fuzz.Ratio(artist.Name, track.AlbumArtist)
                    })
                    .OrderByDescending(match => match.MatchedFor)
                    .Select(match => match.Artist)
                    .FirstOrDefault()
                : data?.ArtistCredit?.FirstOrDefault();
        
        string? artistCountry = !string.IsNullOrWhiteSpace(bestMatchedArtist?.Artist?.Id) ? 
            _musicBrainzAPIService.GetArtistInfoAsync(bestMatchedArtist?.Artist?.Id)?.Country 
            : string.Empty;
        
        string trackBarcode = _mediaTagWriteService.GetTagValue(track, "barcode");
        var matchedReleases =
            data?.Releases
                .Where(release => release.Media?.Any() == true && release.Media?.First()?.Tracks?.Any() == true)
                .Select(release => new
                {
                    Album = release.Title,
                    release.Media?.First()?.Tracks?.First().Title,
                    Length = release.Media?.First()?.Tracks?.First()?.Length / 1000 ?? int.MaxValue,
                    release.Barcode,
                    Release = release
                })
                .Where(release => !string.IsNullOrWhiteSpace(release.Album))
                .Where(release => !string.IsNullOrWhiteSpace(release.Title))
                .Where(release => !string.IsNullOrWhiteSpace(release.Release.Country))
                .Select(release => new
                {
                    Release = release.Release,
                    AlbumMatch = Fuzz.Ratio(release.Album, track.Album),
                    TitleMatch = Fuzz.Ratio(release.Title, track.Title),
                    LengthMatch = Math.Abs(track.Duration - release.Length),
                    CountryMatch = !string.IsNullOrWhiteSpace(artistCountry) ? Fuzz.Ratio(release.Release.Country, artistCountry) : 0,
                    BarcodeMatch = !string.IsNullOrWhiteSpace(release.Barcode) ? Fuzz.Ratio(release.Barcode, trackBarcode) : 0
                })
                .OrderByDescending(match => match.AlbumMatch)
                .ThenByDescending(match => match.TitleMatch)
                .ThenByDescending(match => match.CountryMatch)
                .ThenByDescending(match => match.BarcodeMatch)
                .ThenBy(match => match.LengthMatch)
                .ToList();

        MusicBrainzArtistReleaseModel? release = matchedReleases
            ?.Where(match => match.AlbumMatch > 80)
            ?.Where(match => match.TitleMatch > 80)
            ?.FirstOrDefault()
            ?.Release;
        
        if (release == null || data == null)
        {
            return false;
        }
        
        bool trackInfoUpdated = false;
        
        string? musicBrainzReleaseArtistId = bestMatchedArtist?.Artist?.Id;
        string? musicBrainzAlbumId = release.Id;
        string? musicBrainzReleaseGroupId = release.ReleaseGroup.Id;
        
        string artists = string.Join(';', data?.ArtistCredit.Select(artist => artist.Name));
        string musicBrainzArtistIds = string.Join(';', data?.ArtistCredit.Select(artist => artist.Artist.Id));
        string isrcs = data?.ISRCS != null ? string.Join(';', data?.ISRCS) : string.Empty;

        if (!string.IsNullOrWhiteSpace(release.Id))
        {
            MusicBrainzArtistReleaseModel? withLabeLInfo = _musicBrainzAPIService.GetReleaseWithLabel(release.Id);
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
        }
        
        UpdateTag(track, "date", release.Date, ref trackInfoUpdated);
        UpdateTag(track, "originaldate", release.Date, ref trackInfoUpdated);

        if (string.IsNullOrWhiteSpace(track.Title) || overWriteTrack)
        {
            UpdateTag(track, "Title", release.Media?.FirstOrDefault()?.Title, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.Album) || overWriteAlbum)
        {
            UpdateTag(track, "Album", release.Title, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.AlbumArtist)  || track.AlbumArtist.ToLower().Contains("various") || overwriteAlbumArtist)
        {
            UpdateTag(track, "AlbumArtist", bestMatchedArtist?.Name, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.Artist) || track.Artist.ToLower().Contains("various") || overWriteArtist)
        {
            UpdateTag(track, "Artist", bestMatchedArtist?.Name, ref trackInfoUpdated);
        }

        UpdateTag(track, "ARTISTS", artists, ref trackInfoUpdated);
        UpdateTag(track, "ISRC", isrcs, ref trackInfoUpdated);
        UpdateTag(track, "SCRIPT", release?.TextRepresentation?.Script, ref trackInfoUpdated);
        UpdateTag(track, "barcode", release.Barcode, ref trackInfoUpdated);

        UpdateTag(track, "MusicBrainz Artist Id", musicBrainzArtistIds, ref trackInfoUpdated);

        UpdateTag(track, "MusicBrainz Track Id", recordingId.ToString(), ref trackInfoUpdated);
        
        UpdateTag(track, "MusicBrainz Release Track Id", recordingId, ref trackInfoUpdated);
        UpdateTag(track, "MusicBrainz Release Artist Id", musicBrainzReleaseArtistId, ref trackInfoUpdated);
        UpdateTag(track, "MusicBrainz Release Group Id", musicBrainzReleaseGroupId, ref trackInfoUpdated);
        UpdateTag(track, "MusicBrainz Release Id", release.Id, ref trackInfoUpdated);

        UpdateTag(track, "MusicBrainz Album Artist Id", musicBrainzArtistIds, ref trackInfoUpdated);
        UpdateTag(track, "MusicBrainz Album Id", musicBrainzAlbumId, ref trackInfoUpdated);
        UpdateTag(track, "MusicBrainz Album Type", release.ReleaseGroup.PrimaryType, ref trackInfoUpdated);
        UpdateTag(track, "MusicBrainz Album Release Country", release.Country, ref trackInfoUpdated);
        UpdateTag(track, "MusicBrainz Album Status", release.Status, ref trackInfoUpdated);

        UpdateTag(track, "Acoustid Id", acoustId, ref trackInfoUpdated);
        UpdateTag(track, "Acoustid Fingerprint", fingerprint?.Fingerprint, ref trackInfoUpdated);
        UpdateTag(track, "Acoustid Fingerprint Duration", fingerprint?.Duration.ToString(CultureInfo.InvariantCulture), ref trackInfoUpdated);

        UpdateTag(track, "Date", release.ReleaseGroup.FirstReleaseDate, ref trackInfoUpdated);
        UpdateTag(track, "originaldate", release.ReleaseGroup.FirstReleaseDate, ref trackInfoUpdated);
        
        if (release.ReleaseGroup?.FirstReleaseDate?.Length >= 4)
        {
            UpdateTag(track, "originalyear", release.ReleaseGroup.FirstReleaseDate.Substring(0, 4), ref trackInfoUpdated);
        }
        
        UpdateTag(track, "Disc Number", release.Media?.FirstOrDefault()?.Position?.ToString(), ref trackInfoUpdated);
        UpdateTag(track, "Track Number", release.Media?.FirstOrDefault()?.Tracks?.FirstOrDefault()?.Position?.ToString(), ref trackInfoUpdated);
        UpdateTag(track, "Total Tracks", release.Media?.FirstOrDefault()?.TrackCount.ToString(), ref trackInfoUpdated);
        UpdateTag(track, "MEDIA", release.Media?.FirstOrDefault()?.Format, ref trackInfoUpdated);
        
        return _mediaTagWriteService.SafeSave(track);
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
        
        tagName = _mediaTagWriteService.GetFieldName(track, tagName);
        
        string orgValue = string.Empty;
        bool tempIsUpdated = false;
        _mediaTagWriteService.UpdateTrackTag(track, tagName, value, ref tempIsUpdated, ref orgValue);

        if (tempIsUpdated)
        {
            if (value.Length > 100)
            {
                value = value.Substring(0, 100) + "...";
            }
            
            Console.WriteLine($"Updating tag '{tagName}' value '{orgValue}' => '{value}'");
            trackInfoUpdated = true;
        }
    }
}