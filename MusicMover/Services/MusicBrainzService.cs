using System.Globalization;
using ATL;
using FuzzySharp;
using MusicMover.Models;
using Newtonsoft.Json.Linq;

namespace MusicMover.Services;

public class MusicBrainzService
{
    private readonly string VariousArtists = "Various Artists";
    private readonly string VariousArtistsVA = "VA";
    private readonly MusicBrainzAPIService _musicBrainzAPIService;
    private readonly AcoustIdService _acoustIdService;
    private readonly FingerPrintService _fingerPrintService;
    private readonly MediaTagWriteService _mediaTagWriteService;
    private const int MatchPercentage = 80;
    private const int MinimumArtistName = 5; //prevents very short, non-artist names for example to be used for searching/matching
        
    public MusicBrainzService()
    {
        _musicBrainzAPIService = new MusicBrainzAPIService();
        _acoustIdService = new AcoustIdService();
        _fingerPrintService = new FingerPrintService();
        _mediaTagWriteService = new MediaTagWriteService();
        
    }
    
    public bool WriteTagFromAcoustId(MediaFileInfo mediaFileInfo, FileInfo fromFile, string acoustIdAPIKey,
        bool overWriteArtist, bool overWriteAlbum, bool overWriteTrack, bool overwriteAlbumArtist,
        bool searchByTagNames)
    {
        Track track = new Track(fromFile.FullName);
        FpcalcOutput? fingerprint = _fingerPrintService.GetFingerprint(fromFile.FullName);
        
        string? recordingId = string.Empty;
        string? acoustId = string.Empty;
        
        string? artistCountry = string.Empty;
        MusicBrainzArtistCreditModel? artistCredit = null;
        MusicBrainzArtistReleaseModel? release = null;
        List<MusicBrainzArtistCreditModel> artistCredits = null;
        List<string> listIsrcs = null;

        if (GetDataByAcoustId(fingerprint, mediaFileInfo, fromFile, acoustIdAPIKey, ref recordingId, ref acoustId))
        {
            var data = _musicBrainzAPIService.GetRecordingById(recordingId);
            if (data != null)
            {
                artistCredit = GetBestMatchingArtist(data.ArtistCredit, track);
            
                artistCountry = !string.IsNullOrWhiteSpace(artistCredit?.Artist?.Id) ? 
                    _musicBrainzAPIService.GetArtistInfo(artistCredit.Artist.Id)?.Country 
                    : string.Empty;
        
                release = GetBestMatchingRelease(data, track, artistCountry, artistCredit?.Name);
                artistCredits = data.ArtistCredit;
                listIsrcs = data.ISRCS;
            }
        }

        if (release == null && searchByTagNames)
        {
            MusicBrainzRecordingQueryEntityModel recordingQuery = null;
            release = SearchBestMatchingRelease(track, ref artistCredit, ref artistCountry, ref recordingQuery);
                
            if (release == null)
            {
                Console.WriteLine($"MusicBrainz recording not found by id '{recordingId}' by searching from tag names, Artist: {track.Artist}, ALbum: {track.Album}, Title: {track.Title}");
                return false;
            }

            artistCredits = recordingQuery.ArtistCredit;
            listIsrcs = recordingQuery.ISRCS;
        }

        if (release == null)
        {
            return false;
        }
        
        bool trackInfoUpdated = false;
        
        string? musicBrainzReleaseArtistId = artistCredit?.Artist?.Id;
        string? musicBrainzAlbumId = release.Id;
        string? musicBrainzReleaseGroupId = release.ReleaseGroup.Id;
        
        string artists = string.Join(';', artistCredits.Select(artist => artist.Name));
        string musicBrainzArtistIds = string.Join(';', artistCredits.Select(artist => artist.Artist.Id));
        string isrcs = listIsrcs != null ? string.Join(';', listIsrcs) : string.Empty;

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
            UpdateTag(track, "Title", release.Media?.FirstOrDefault()?.Tracks.FirstOrDefault().Title, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.Album) || overWriteAlbum)
        {
            if (!release.Title.ToLower().Contains(release.Disambiguation.ToLower()) &&
                track.Album.ToLower().Contains(release.Disambiguation.ToLower()) &&
                !release.Title.Trim().EndsWith(")"))
            {
                string disambiguation = string.Join(' ',
                    release.Disambiguation
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(dis => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(dis)));
                release.Title += $" ({disambiguation})";
            } 
            
            UpdateTag(track, "Album", release.Title, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.AlbumArtist)  || track.AlbumArtist.ToLower().Contains("various") || overwriteAlbumArtist)
        {
            UpdateTag(track, "AlbumArtist", artistCredit?.Name, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.Artist) || track.Artist.ToLower().Contains("various") || overWriteArtist)
        {
            UpdateTag(track, "Artist", artistCredit?.Name, ref trackInfoUpdated);
        }

        UpdateTag(track, "ARTISTS", artists, ref trackInfoUpdated);
        UpdateTag(track, "ISRC", isrcs, ref trackInfoUpdated);
        UpdateTag(track, "SCRIPT", release?.TextRepresentation?.Script, ref trackInfoUpdated);
        UpdateTag(track, "barcode", release.Barcode, ref trackInfoUpdated);

        UpdateTag(track, "MusicBrainz Artist Id", musicBrainzArtistIds, ref trackInfoUpdated);

        UpdateTag(track, "MusicBrainz Track Id", recordingId, ref trackInfoUpdated);
        
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
            if (orgValue.Length > 100)
            {
                orgValue = orgValue.Substring(0, 100) + "...";
            }
            
            Console.WriteLine($"Updating tag '{tagName}' value '{orgValue}' => '{value}'");
            trackInfoUpdated = true;
        }
    }

    private AcoustIdRecordingResponse? GetBestMatchingAcoustId(AcoustIdResponse? acoustIdResponse, 
        MediaFileInfo mediaFileInfo,
        ref string acoustId)
    {
        if (acoustIdResponse?.Results?.Count == 0)
        {
            return null;
        }

        var highestScoreResult = acoustIdResponse
            .Results
            .Where(result => result.Recordings?.Any() == true)
            .OrderByDescending(result => result.Score)
            .FirstOrDefault();

        if (highestScoreResult == null)
        {
            return null;
        }

        acoustId = highestScoreResult.Id;

        return highestScoreResult
            .Recordings
            ?.Select(result => new
            {
                ArtistMatchedFor = result.Artists?.Sum(artist => Fuzz.Ratio(mediaFileInfo.Artist?.ToLower(), artist.Name?.ToLower())) ?? 0,
                TitleMatchedFor = Fuzz.Ratio(mediaFileInfo.Title?.ToLower(), result.Title?.ToLower()),
                LengthMatch = Math.Abs(mediaFileInfo.Duration - result.Duration ?? 100),
                Result = result
            })
            .OrderByDescending(result => result.ArtistMatchedFor)
            .ThenByDescending(result => result.TitleMatchedFor)
            .ThenBy(result => result.LengthMatch)
            .Select(result => result.Result)
            .FirstOrDefault();
    }

    private MusicBrainzArtistCreditModel? GetBestMatchingArtist(List<MusicBrainzArtistCreditModel>? artists, Track track)
    {
        return !string.IsNullOrWhiteSpace(track.AlbumArtist)
                ? artists
                    ?.Where(artist => !string.Equals(artist.Name, VariousArtists, StringComparison.OrdinalIgnoreCase))
                    .Select(artist => new
                    {
                        Artist = artist,
                        MatchedFor = Fuzz.Ratio(artist.Name?.ToLower(), track.AlbumArtist?.ToLower()) + Fuzz.Ratio(artist.Name?.ToLower(), track.Artist?.ToLower())
                    })
                    .OrderByDescending(match => match.MatchedFor)
                    .Select(match => match.Artist)
                    .FirstOrDefault()
                : artists
                    ?.Where(artist => !string.Equals(artist.Name, VariousArtists, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
    }

    private MusicBrainzArtistReleaseModel? GetBestMatchingRelease(MusicBrainzArtistModel? data, 
        Track track, 
        string artistCountry,
        string targetArtist)
    {
        if (data == null)
        {
            return null;
        }

        string trackArtist = IsVariousArtists(track.Artist) ? targetArtist : track.Artist;
        string trackAlbum = IsVariousArtists(track.Album) ? string.Empty : track.Album;
        
        string trackBarcode = _mediaTagWriteService.GetTagValue(track, "barcode");
        
        var matchedReleases =
            data?.Releases
                .Where(release => release.Media.Any())
                .Select(release => new
                {
                    Album = release.Title,
                    release.Barcode,
                    Release = release
                })
                .Where(release => !string.IsNullOrWhiteSpace(release.Album))
                .Where(release => !string.IsNullOrWhiteSpace(release.Release.Country))
                .Select(release => new
                {
                    release.Release,
                    AlbumMatch = !string.IsNullOrWhiteSpace(trackAlbum) ? Math.Max(
                        Fuzz.Ratio($"{release.Album?.ToLower()} {release.Release.Disambiguation}", trackAlbum?.ToLower()), //search with added "deluxe edition" etc
                        Fuzz.Ratio(release.Album?.ToLower(), trackAlbum?.ToLower())) : 100,
                    ArtistMatch = !string.IsNullOrWhiteSpace(trackArtist) ? data.ArtistCredit.Sum(artist => Fuzz.Ratio(trackArtist?.ToLower(), artist.Name?.ToLower())) : 100,
                    CountryMatch = !string.IsNullOrWhiteSpace(artistCountry) ? Fuzz.Ratio(release.Release.Country?.ToLower(), artistCountry?.ToLower()) : 0,
                    BarcodeMatch = !string.IsNullOrWhiteSpace(release.Barcode) ? Fuzz.Ratio(release.Barcode?.ToLower(), trackBarcode?.ToLower()) : 0
                })
                .OrderByDescending(match => match.AlbumMatch)
                .ThenByDescending(match => match.CountryMatch)
                .ThenByDescending(match => match.BarcodeMatch)
                .ToList();
        
        var potentialReleases = matchedReleases
            .Where(match => match.AlbumMatch > MatchPercentage)
            .Where(match => match.ArtistMatch > MatchPercentage)
            .ToList();

        foreach (var potentialRelease in potentialReleases)
        {
            foreach (var media in potentialRelease.Release.Media)
            {
                var tempTracks = media.Tracks;
                media.Tracks = GetBestMatchingTracks(tempTracks, track.Title);

                if (media?.Tracks?.Count == 0 && track.Title.ToLower().Contains(trackArtist.ToLower()))
                {
                    //try without the artist name in the title for a match
                    string withoutArtistName = track.Title.ToLower().Replace(trackArtist.ToLower(), string.Empty);
                    if (withoutArtistName.Length >= MinimumArtistName)
                    {
                        media.Tracks = GetBestMatchingTracks(tempTracks, withoutArtistName);
                    }
                }
            }
        }

        var bestMatchRelease = potentialReleases
            .Where(release => release.Release.Media.Sum(media => media?.Tracks?.Count ?? 0) > 0)
            .Select(release => release.Release)
            .FirstOrDefault();

        return bestMatchRelease;
    }

    private List<MusicBrainzReleaseMediaTrackModel>? GetBestMatchingTracks(List<MusicBrainzReleaseMediaTrackModel>? tracks, string trackTitle)
    {
        return tracks?
            .Select(releaseTrack => new
            {
                Track = releaseTrack,
                Length = releaseTrack.Length / 1000 ?? int.MaxValue,
                TitleMatch = Fuzz.Ratio(trackTitle.ToLower(), releaseTrack.Title?.ToLower())
            })
            .Where(match => match.TitleMatch > MatchPercentage)
            .OrderByDescending(releaseTrack => releaseTrack.TitleMatch)
            .ThenBy(releaseTrack => releaseTrack.Length)
            .Select(releaseTrack => releaseTrack.Track)
            .ToList();
    }

    private MusicBrainzRecordingQueryReleaseEntityModel? GetBestMatchingRelease(MusicBrainzRecordingQueryEntityModel? data, Track track, string trackArtist)
    {
        if (data == null)
        {
            return null;
        }
        
        string trackAlbumWithoutArtist = track.Album.ToLower().Contains(trackArtist.ToLower()) ? track.Album.ToLower().Replace(trackArtist.ToLower(), string.Empty) : track.Album;
        
        var matchedReleases =
            data.Releases
                .Where(release => release.Media.Any())
                .Select(release => new
                {
                    Album = release.Title,
                    Release = release
                })
                .Where(release => !string.IsNullOrWhiteSpace(release.Album))
                .Where(release => !string.IsNullOrWhiteSpace(release.Release.Country))
                .Select(release => new
                {
                    release.Release,
                    ArtistMatch = data.ArtistCredit.Sum(artist => Fuzz.Ratio(track.Artist?.ToLower(), artist.Name?.ToLower())),
                    AlbumMatch = !string.IsNullOrWhiteSpace(track.Album) ? Math.Max(Fuzz.Ratio(release.Album?.ToLower(), track.Album?.ToLower()), 
                                                                                    Fuzz.Ratio(release.Album?.ToLower(), trackAlbumWithoutArtist)): 100
                    
                    
                })
                .OrderByDescending(match => match.ArtistMatch)
                .ThenByDescending(match => match.AlbumMatch)
                .ToList();

        return matchedReleases
            .Where(match => match.AlbumMatch > MatchPercentage)
            .Where(match => match.ArtistMatch > MatchPercentage)
            .Select(match => match.Release)
            .FirstOrDefault();
    }

    private MusicBrainzArtistReleaseModel? SearchBestMatchingRelease(Track track, 
        ref MusicBrainzArtistCreditModel bestMatchedArtist, 
        ref string? artistCountry, 
        ref MusicBrainzRecordingQueryEntityModel recordingQuery)
    {
        string trackArtist = IsVariousArtists(track.Artist) ? bestMatchedArtist.Name : track.Artist;

        if (string.IsNullOrWhiteSpace(trackArtist))
        {
            Console.WriteLine("Unable to search for a track without the artist name on MusicBrainz");
            return null;
        }
        
        var searchResult = _musicBrainzAPIService.SearchRelease(track.Artist, track.Album, track.Title);
        
        if (searchResult?.Recordings?.Count == 0 && 
            (track.Title.ToLower().Contains(trackArtist.ToLower()) ||
             track.Album.ToLower().Contains(trackArtist.ToLower())))
        {
            //try without the artist name in the title for a match
            string titleWithoutArtistName = track.Title.ToLower().Replace(trackArtist.ToLower(), string.Empty);
            string albumWithoutArtistName = track.Album.ToLower().Replace(trackArtist.ToLower(), string.Empty);

            if (!string.IsNullOrWhiteSpace(bestMatchedArtist?.JoinPhrase) &&
                titleWithoutArtistName.Contains(bestMatchedArtist.JoinPhrase.Trim().ToLower()))
            {
                titleWithoutArtistName = titleWithoutArtistName.Substring(0, titleWithoutArtistName.IndexOf(bestMatchedArtist.JoinPhrase.Trim().ToLower()));
            }
            
            //cleanup some characters that might fail the search
            titleWithoutArtistName = titleWithoutArtistName
                .Replace("[", string.Empty)
                .Replace("]", string.Empty)
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace(":", string.Empty)
                .Trim();
            
            if (titleWithoutArtistName.Length >= MinimumArtistName && 
                albumWithoutArtistName.Length >= MinimumArtistName)
            {
                searchResult = _musicBrainzAPIService.SearchRelease(track.Artist, albumWithoutArtistName, titleWithoutArtistName);
            }
        }
        
        foreach (var recording in searchResult.Recordings)
        {
            var tempRelease = GetBestMatchingRelease(recording, track, trackArtist);
            if (tempRelease == null)
            {
                continue;
            }

            recordingQuery = recording;
            var matchedArtist = GetBestMatchingArtist(tempRelease.ArtistCredit, track);
            if (matchedArtist != null)
            {
                bestMatchedArtist = matchedArtist;
                
                artistCountry = !string.IsNullOrWhiteSpace(bestMatchedArtist?.Artist?.Id) ? 
                    _musicBrainzAPIService.GetArtistInfo(bestMatchedArtist.Artist.Id)?.Country 
                    : string.Empty;
            }
            
            var mbTempRelease = _musicBrainzAPIService.GetReleaseWithAll(tempRelease.Id);
            if (mbTempRelease != null)
            {
                MusicBrainzArtistModel musicBrainzArtistModel = new MusicBrainzArtistModel();
                musicBrainzArtistModel.Releases.Add(mbTempRelease);

                if (bestMatchedArtist != null)
                {
                    musicBrainzArtistModel.ArtistCredit.Add(bestMatchedArtist);
                }
                
                var release = GetBestMatchingRelease(musicBrainzArtistModel, track, artistCountry, trackArtist);

                if (release != null)
                {
                    return release;
                }
            }
        }
        
        return null;
    }

    private bool GetDataByAcoustId(FpcalcOutput? fingerprint, 
        MediaFileInfo mediaFileInfo, 
        FileInfo fromFile, 
        string acoustIdAPIKey,
        ref string recordingId, 
        ref string acoustId)
    {
        if (fingerprint is null)
        {
            Console.WriteLine("Failed to generate fingerprint, corrupt file?");
        }
        
        if (fingerprint is null && !string.IsNullOrWhiteSpace(mediaFileInfo.AcoustId))
        {
            Console.WriteLine($"Looking up AcoustID provided by AcoustId Tag, '{fromFile.FullName}'");
            
            //try again but with the AcoustID from the media file
            var acoustIdLookup = _acoustIdService.LookupByAcoustId(acoustIdAPIKey, mediaFileInfo.AcoustId);
            var acoustidRecording = GetBestMatchingAcoustId(acoustIdLookup, mediaFileInfo, ref acoustId);
            recordingId = acoustidRecording?.Id;
            
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
            var acoustIdLookup = _acoustIdService.LookupAcoustId(acoustIdAPIKey, fingerprint.Fingerprint, (int)fingerprint.Duration);
            var acoustidRecording = GetBestMatchingAcoustId(acoustIdLookup, mediaFileInfo, ref acoustId);
            recordingId = acoustidRecording?.Id;

            if (!string.IsNullOrWhiteSpace(mediaFileInfo.AcoustId) && 
                (string.IsNullOrWhiteSpace(recordingId) || string.IsNullOrWhiteSpace(acoustId)))
            {
                Console.WriteLine($"Looking up AcoustID provided by AcoustId Tag, '{fromFile.FullName}'");
            
                //try again but with the AcoustID from the media file
                acoustIdLookup = _acoustIdService.LookupByAcoustId(acoustIdAPIKey, mediaFileInfo.AcoustId);
                acoustidRecording = GetBestMatchingAcoustId(acoustIdLookup, mediaFileInfo, ref acoustId);
                recordingId = acoustidRecording?.Id;
            
                if (!string.IsNullOrWhiteSpace(recordingId) && !string.IsNullOrWhiteSpace(acoustId))
                {
                    Console.WriteLine($"Found AcoustId info from the AcoustId service by the AcoustID provided by the media Tag, '{fromFile.FullName}'");
                }
            }
        }
        
        if (string.IsNullOrWhiteSpace(recordingId) || string.IsNullOrWhiteSpace(acoustId))
        {
            Console.WriteLine($"MusicBrainz recording not found by id '{recordingId}'");
            return false;
        }

        return true;
    }

    private bool IsVariousArtists(string? name)
    {
        if (name?.ToLower() == VariousArtistsVA.ToLower())
        {
            return true;
        }

        if (Fuzz.Ratio(name, VariousArtists) >= 95)
        {
            return true;
        }

        return false;
    }
}