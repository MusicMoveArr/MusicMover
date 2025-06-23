using System.Globalization;
using ATL;
using FuzzySharp;
using MusicMover.Helpers;
using MusicMover.Models;
using MusicMover.Models.MusicBrainz;
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
    private const int MinimumArtistName = 2; //prevents very short, non-artist names for example to be used for searching/matching
        
    public MusicBrainzService()
    {
        _musicBrainzAPIService = new MusicBrainzAPIService();
        _acoustIdService = new AcoustIdService();
        _fingerPrintService = new FingerPrintService();
        _mediaTagWriteService = new MediaTagWriteService();
        
    }
    
    public async Task<bool> WriteTagFromAcoustIdAsync(MediaFileInfo mediaFileInfo, FileInfo fromFile, string acoustIdAPIKey,
        bool overWriteArtist, bool overWriteAlbum, bool overWriteTrack, bool overwriteAlbumArtist,
        bool searchByTagNames)
    {
        FpcalcOutput? fingerprint = await _fingerPrintService.GetFingerprintAsync(fromFile.FullName);
        
        string? recordingId = string.Empty;
        string? acoustId = string.Empty;
        
        string? artistCountry = string.Empty;
        MusicBrainzArtistCreditModel? artistCredit = null;
        MusicBrainzArtistReleaseModel? release = null;
        List<MusicBrainzArtistCreditModel>? artistCredits = null;
        List<string>? listIsrcs = null;
        GetDataByAcoustIdResult acoustIdResult = await GetDataByAcoustIdAsync(fingerprint, mediaFileInfo, fromFile, acoustIdAPIKey);

        if (acoustIdResult.Success)
        {
            var data = await _musicBrainzAPIService.GetRecordingByIdAsync(acoustIdResult.RecordingId);
            if (data != null)
            {
                artistCredit = GetBestMatchingArtist(data.ArtistCredit, mediaFileInfo.TrackInfo);
            
                artistCountry = !string.IsNullOrWhiteSpace(artistCredit?.Artist?.Id) ? 
                    (await _musicBrainzAPIService.GetArtistInfoAsync(artistCredit.Artist.Id))?.Country 
                    : string.Empty;
        
                release = GetBestMatchingRelease(data, mediaFileInfo.TrackInfo, artistCountry, artistCredit?.Name, false);

                if (release == null)
                {
                    data = await _musicBrainzAPIService.GetRecordingByIdAsync(acoustIdResult.RecordingId);
                    mediaFileInfo.TrackInfo.Title = acoustIdResult.MatchedRecording?.Title;
                    
                    release = GetBestMatchingRelease(data, mediaFileInfo.TrackInfo, artistCountry, artistCredit?.Name, true);
                    mediaFileInfo.TrackInfo.Title = mediaFileInfo.Title;
                }
                
                artistCredits = data.ArtistCredit;
                listIsrcs = data.ISRCS;
            }
        }

        if (release == null && searchByTagNames)
        {
            SearchBestMatchingReleaseResult matchingReleaseResult = await SearchBestMatchingReleaseAsync(mediaFileInfo.TrackInfo, artistCredit, false);

            if (!matchingReleaseResult.Success)
            {
                Console.WriteLine($"MusicBrainz recording not found by id '{recordingId}' by searching from tag names, Artist: {mediaFileInfo.Artist}, ALbum: {mediaFileInfo.Album}, Title: {mediaFileInfo.Title}");
                return false;
            }

            artistCredits = matchingReleaseResult.RecordingQuery.ArtistCredit;
            listIsrcs = matchingReleaseResult.RecordingQuery.ISRCS;
            release = matchingReleaseResult.MatchedRelease;
        }

        MusicBrainzReleaseMediaModel? releaseMedia = release
            ?.Media
            ?.FirstOrDefault();

        if (releaseMedia == null)
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
            MusicBrainzArtistReleaseModel? withLabeLInfo = await _musicBrainzAPIService.GetReleaseWithLabelAsync(release.Id);
            var label = withLabeLInfo?.LabeLInfo?.FirstOrDefault(label => label?.Label?.Type?.ToLower().Contains("production") == true);

            if (label == null && withLabeLInfo?.LabeLInfo?.Count == 1)
            {
                label = withLabeLInfo?.LabeLInfo?.FirstOrDefault();
            }
            if (!string.IsNullOrWhiteSpace(label?.Label?.Name))
            {
                UpdateTag(mediaFileInfo.TrackInfo, "LABEL", label?.Label.Name, ref trackInfoUpdated);
                UpdateTag(mediaFileInfo.TrackInfo, "CATALOGNUMBER", label?.CataLogNumber, ref trackInfoUpdated);
            }
        }
        
        UpdateTag(mediaFileInfo.TrackInfo, "date", release.Date, ref trackInfoUpdated);
        UpdateTag(mediaFileInfo.TrackInfo, "originaldate", release.Date, ref trackInfoUpdated);

        if (string.IsNullOrWhiteSpace(mediaFileInfo.Title) || overWriteTrack)
        {
            string? trackTitle = releaseMedia?.Tracks?.FirstOrDefault()?.Title;
            string credits = GetArtistFeatCreditString(artistCredits, artistCredit?.Name, true);
            if (credits.Length > 2)
            {
                trackTitle += " " + credits;
            }
            UpdateTag(mediaFileInfo.TrackInfo, "Title", trackTitle, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(mediaFileInfo.Album) || overWriteAlbum)
        {
            if (!release.Title.ToLower().Contains(release.Disambiguation.ToLower()) &&
                mediaFileInfo.Album.ToLower().Contains(release.Disambiguation.ToLower()) &&
                !release.Title.Trim().EndsWith(")"))
            {
                string disambiguation = string.Join(' ',
                    release.Disambiguation
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(dis => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(dis)));
                release.Title += $" ({disambiguation})";
            } 
            
            UpdateTag(mediaFileInfo.TrackInfo, "Album", release.Title, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(mediaFileInfo.AlbumArtist)  || mediaFileInfo.AlbumArtist.ToLower().Contains("various") || overwriteAlbumArtist)
        {
            UpdateTag(mediaFileInfo.TrackInfo, "AlbumArtist", artistCredit?.Name, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(mediaFileInfo.Artist) || mediaFileInfo.Artist.ToLower().Contains("various") || overWriteArtist)
        {
            UpdateTag(mediaFileInfo.TrackInfo, "Artist", artistCredit?.Name, ref trackInfoUpdated);
        }

        UpdateTag(mediaFileInfo.TrackInfo, "ARTISTS", artists, ref trackInfoUpdated);
        UpdateTag(mediaFileInfo.TrackInfo, "ISRC", isrcs, ref trackInfoUpdated);
        UpdateTag(mediaFileInfo.TrackInfo, "SCRIPT", release?.TextRepresentation?.Script, ref trackInfoUpdated);
        UpdateTag(mediaFileInfo.TrackInfo, "barcode", release.Barcode, ref trackInfoUpdated);

        UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Artist Id", musicBrainzArtistIds, ref trackInfoUpdated);

        UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Track Id", recordingId, ref trackInfoUpdated);
        
        UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Release Track Id", recordingId, ref trackInfoUpdated);
        UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Release Artist Id", musicBrainzReleaseArtistId, ref trackInfoUpdated);
        UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Release Group Id", musicBrainzReleaseGroupId, ref trackInfoUpdated);
        UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Release Id", release.Id, ref trackInfoUpdated);

        UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Album Artist Id", musicBrainzArtistIds, ref trackInfoUpdated);
        UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Album Id", musicBrainzAlbumId, ref trackInfoUpdated);
        UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Album Type", release.ReleaseGroup.PrimaryType, ref trackInfoUpdated);
        UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Album Release Country", release.Country, ref trackInfoUpdated);
        UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Album Status", release.Status, ref trackInfoUpdated);

        UpdateTag(mediaFileInfo.TrackInfo, "Acoustid Id", acoustId, ref trackInfoUpdated);
        UpdateTag(mediaFileInfo.TrackInfo, "Acoustid Fingerprint", fingerprint?.Fingerprint, ref trackInfoUpdated);
        UpdateTag(mediaFileInfo.TrackInfo, "Acoustid Fingerprint Duration", fingerprint?.Duration.ToString(CultureInfo.InvariantCulture), ref trackInfoUpdated);

        UpdateTag(mediaFileInfo.TrackInfo, "Date", release.ReleaseGroup.FirstReleaseDate, ref trackInfoUpdated);
        UpdateTag(mediaFileInfo.TrackInfo, "originaldate", release.ReleaseGroup.FirstReleaseDate, ref trackInfoUpdated);
        
        if (release.ReleaseGroup?.FirstReleaseDate?.Length >= 4)
        {
            UpdateTag(mediaFileInfo.TrackInfo, "originalyear", release.ReleaseGroup.FirstReleaseDate.Substring(0, 4), ref trackInfoUpdated);
        }
        
        UpdateTag(mediaFileInfo.TrackInfo, "Disc Number", releaseMedia.Position?.ToString(), ref trackInfoUpdated);
        UpdateTag(mediaFileInfo.TrackInfo, "Track Number", releaseMedia.Tracks?.FirstOrDefault()?.Position?.ToString(), ref trackInfoUpdated);
        UpdateTag(mediaFileInfo.TrackInfo, "Total Tracks", releaseMedia.TrackCount.ToString(), ref trackInfoUpdated);
        UpdateTag(mediaFileInfo.TrackInfo, "MEDIA", releaseMedia.Format, ref trackInfoUpdated);

        return await _mediaTagWriteService.SafeSaveAsync(mediaFileInfo.TrackInfo);
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
        ref string? acoustId)
    {
        if (acoustIdResponse?.Results?.Count == 0)
        {
            return null;
        }

        var highestScoreResult = acoustIdResponse
            ?.Results
            ?.Where(result => result.Recordings?.Any() == true)
            .Where(result => result.Score >= (MatchPercentage / 100F))
            .OrderByDescending(result => result.Score)
            .FirstOrDefault();

        if (highestScoreResult == null)
        {
            return null;
        }

        acoustId = highestScoreResult.Id;

        //perhaps not the best approach but sometimes...
        bool ignoreFilters = string.IsNullOrWhiteSpace(mediaFileInfo.Album) ||
                             string.IsNullOrWhiteSpace(mediaFileInfo.Artist) ||
                             string.IsNullOrWhiteSpace(mediaFileInfo.Title);

        var results = highestScoreResult
            .Recordings
            ?.Select(result => new
            {
                ArtistMatchedFor = result.Artists?.Sum(artist => Fuzz.TokenSortRatio(artist.Name?.ToLower(), mediaFileInfo.Artist?.ToLower())) ?? 0,
                //ArtistMatchedFor = result.Artists?.Count > 0 ? Fuzz.TokenSortRatio(mediaFileInfo.Artist?.ToLower(), string.Join(',', result.Artists)) : 0,
                TitleMatchedFor = Fuzz.TokenSortRatio(mediaFileInfo.Title?.ToLower(), result.Title?.ToLower()),
                LengthMatch = Math.Abs(mediaFileInfo.Duration - result.Duration ?? 100),
                Result = result
            })
            .Where(match => ignoreFilters || FuzzyHelper.ExactNumberMatch(mediaFileInfo.Title, match.Result.Title))
            //.Where(match => ignoreFilters || match.ArtistMatchedFor >= MatchPercentage)
            //.Where(match => ignoreFilters || match.TitleMatchedFor >= MatchPercentage)
            .OrderByDescending(result => result.ArtistMatchedFor)
            .ThenByDescending(result => result.TitleMatchedFor)
            .ThenBy(result => result.LengthMatch)
            //.Select(result => result.Result)
            .ToList();

        AcoustIdRecordingResponse? firstResult = results?.FirstOrDefault()?.Result;

        return firstResult;
    }

    private MusicBrainzArtistCreditModel? GetBestMatchingArtist(List<MusicBrainzArtistCreditModel>? artists, Track track)
    {
        string[] splitTrackArtists = !string.IsNullOrWhiteSpace(track.AlbumArtist) ? 
                                        track.AlbumArtist.Split([',', ';', '&'], StringSplitOptions.RemoveEmptyEntries) : [];

        if (splitTrackArtists.Length > 0)
        {
            foreach (string splitArtist in splitTrackArtists)
            {
                var foundArtist = artists
                    ?.Where(artist => !string.Equals(artist.Name, VariousArtists, StringComparison.OrdinalIgnoreCase))
                    .Select(artist => new
                    {
                        Artist = artist,
                        MatchedFor = Fuzz.Ratio(artist.Name?.ToLower(), splitArtist.ToLower())
                    })
                    .Where(match => match.MatchedFor >= MatchPercentage)
                    .OrderByDescending(match => match.MatchedFor)
                    .Select(match => match.Artist)
                    .FirstOrDefault();

                if (foundArtist != null)
                {
                    return foundArtist;
                }
            }
        }
        
        return artists
            ?.Where(artist => !string.Equals(artist.Name, VariousArtists, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    private MusicBrainzArtistReleaseModel? GetBestMatchingRelease(MusicBrainzArtistModel? data, 
        Track track, 
        string? artistCountry,
        string targetArtist,
        bool relaxedFiltering)
    {
        if (data == null)
        {
            return null;
        }

        string trackAlbum = IsVariousArtists(track.Album) ? string.Empty : track.Album;
        string trackBarcode = _mediaTagWriteService.GetTagValue(track, "barcode");
        
        var matchedReleases =
            data.Releases
                ?.Where(release => release.Media.Any())
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
                    AlbumName = release.Album,
                    release.Release,
                    AlbumMatch = !string.IsNullOrWhiteSpace(trackAlbum) ? Math.Max(
                        relaxedFiltering ? Fuzz.PartialTokenSortRatio($"{release.Album?.ToLower()} {release.Release.Disambiguation}", trackAlbum?.ToLower()) : Fuzz.Ratio($"{release.Album?.ToLower()} {release.Release.Disambiguation}", trackAlbum?.ToLower()), //search with added "deluxe edition" etc
                        relaxedFiltering ? Fuzz.PartialTokenSortRatio(release.Album?.ToLower(), trackAlbum?.ToLower()) : Fuzz.Ratio(release.Album?.ToLower(), trackAlbum?.ToLower())) : 100,
                    ArtistMatch = !string.IsNullOrWhiteSpace(targetArtist) ? data.ArtistCredit.Sum(artist => Fuzz.Ratio(targetArtist.ToLower(), artist.Name?.ToLower())) : 100,
                    CountryMatch = !string.IsNullOrWhiteSpace(artistCountry) ? relaxedFiltering ? Fuzz.PartialTokenSortRatio(release.Release.Country?.ToLower(), artistCountry?.ToLower()) : Fuzz.Ratio(release.Release.Country?.ToLower(), artistCountry?.ToLower()) : 0,
                    BarcodeMatch = !string.IsNullOrWhiteSpace(release.Barcode) ? relaxedFiltering ? Fuzz.PartialTokenSortRatio(release.Barcode?.ToLower(), trackBarcode?.ToLower()) : Fuzz.Ratio(release.Barcode?.ToLower(), trackBarcode?.ToLower()) : 0
                })
                //.Where(match => relaxedFiltering || FuzzyHelper.ExactNumberMatch(trackAlbum, match.AlbumName))
                .OrderByDescending(match => match.AlbumMatch)
                .ThenByDescending(match => match.CountryMatch)
                .ThenByDescending(match => match.BarcodeMatch)
                .ToList();
        
        var potentialReleases = matchedReleases
            ?.Where(match => match.AlbumMatch >= MatchPercentage)
            ?.Where(match => match.ArtistMatch >= MatchPercentage)
            ?.ToList() ?? [];

        foreach (var potentialRelease in potentialReleases)
        {
            foreach (var media in potentialRelease.Release.Media)
            {
                var tempTracks = media.Tracks;
                media.Tracks = GetBestMatchingTracks(tempTracks, track.Title, string.Empty, relaxedFiltering);

                if (media?.Tracks?.Count == 0 && 
                    !string.IsNullOrWhiteSpace(targetArtist) &&
                    track.Title.ToLower().Contains(targetArtist.ToLower()))
                {
                    //try without the artist name in the title for a match
                    string withoutArtistName = track.Title.ToLower().Replace(targetArtist.ToLower(), string.Empty);
                    if (withoutArtistName.Length >= MinimumArtistName)
                    {
                        media.Tracks = GetBestMatchingTracks(tempTracks, withoutArtistName, string.Empty, relaxedFiltering);
                    }
                }

                if (media?.Tracks?.Count == 0 && data?.ArtistCredit?.Count > 1)
                {
                    //try by adding artist credit join phrase
                    string artistCredits = GetArtistFeatCreditString(data.ArtistCredit, string.Empty, true);
                    media.Tracks = GetBestMatchingTracks(tempTracks, track.Title, artistCredits, relaxedFiltering);
                }

                if (media?.Tracks?.Count == 0 &&
                    data?.ArtistCredit?.Count > 1 &&
                    !string.IsNullOrWhiteSpace(data.ArtistCredit.First().JoinPhrase))
                {
                    //try by removing the join phrase
                    string titleWithoutCredit = CleanupArtistCredit(track.Title, data?.ArtistCredit?.First()?.JoinPhrase);
                    
                    if (!string.IsNullOrWhiteSpace(titleWithoutCredit))
                    {
                        media.Tracks = GetBestMatchingTracks(tempTracks, titleWithoutCredit, string.Empty, relaxedFiltering);
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

    private List<MusicBrainzReleaseMediaTrackModel>? GetBestMatchingTracks(List<MusicBrainzReleaseMediaTrackModel>? tracks, 
        string trackTitle, 
        string artistCredit, 
        bool relaxedFiltering)
    {
        return tracks?
            .Select(releaseTrack => new
            {
                Track = releaseTrack,
                Length = releaseTrack.Length / 1000 ?? int.MaxValue,
                TitleMatch = relaxedFiltering 
                    ? Fuzz.PartialTokenSortRatio(trackTitle.ToLower(), releaseTrack.Title?.ToLower() + artistCredit)
                    : Fuzz.Ratio(trackTitle.ToLower(), releaseTrack.Title?.ToLower() + artistCredit)
            })
            //.Where(match => match.Track.Recording != null)
            .Where(match => match.TitleMatch >= MatchPercentage)
            .Where(match => relaxedFiltering || FuzzyHelper.ExactNumberMatch(trackTitle, match.Track.Title))
            .OrderByDescending(releaseTrack => releaseTrack.TitleMatch)
            .ThenBy(releaseTrack => releaseTrack.Length)
            .Select(releaseTrack => releaseTrack.Track)
            .ToList();
    }

    private MusicBrainzRecordingQueryReleaseEntityModel? GetBestMatchingRecordingRelease(
        MusicBrainzRecordingQueryEntityModel? data, 
        Track track, 
        string trackArtist)
    {
        if (data == null)
        {
            return null;
        }
        
        string trackAlbumWithoutArtist = track.Album.ToLower().Contains(trackArtist.ToLower()) ? track.Album.ToLower().Replace(trackArtist.ToLower(), string.Empty) : track.Album;
        
        var matchedReleases =
            data?.Releases
                ?.Where(release => release.Media.Any())
                .Select(release => new
                {
                    Album = release.Title,
                    Release = release
                })
                .Where(release => !string.IsNullOrWhiteSpace(release.Album))
                .Where(release => !string.IsNullOrWhiteSpace(release.Release.Country))
                .Select(release => new
                {
                    AlbumName = release.Album,
                    release.Release,
                    ArtistMatch = data.ArtistCredit.Sum(artist => Fuzz.Ratio(track.Artist?.ToLower(), artist.Name?.ToLower())),
                    AlbumMatch = !string.IsNullOrWhiteSpace(track.Album) ? Math.Max(Fuzz.Ratio(release.Album?.ToLower(), track.Album?.ToLower()), 
                                                                                    Fuzz.Ratio(release.Album?.ToLower(), trackAlbumWithoutArtist)): 100
                    
                    
                })
                .Where(match => FuzzyHelper.ExactNumberMatch(track.Album, match.AlbumName))
                .OrderByDescending(match => match.ArtistMatch)
                .ThenByDescending(match => match.AlbumMatch)
                .ToList();

        return matchedReleases
            ?.Where(match => match.AlbumMatch >= MatchPercentage)
            .Where(match => match.ArtistMatch >= MatchPercentage)
            .Select(match => match.Release)
            .FirstOrDefault();
    }

    private async Task<SearchBestMatchingReleaseResult> SearchBestMatchingReleaseAsync(Track track,
        MusicBrainzArtistCreditModel? bestMatchedArtist,
        bool relaxedFiltering)
    {
        SearchBestMatchingReleaseResult result = new SearchBestMatchingReleaseResult();
        result.BestMatchedArtist = bestMatchedArtist;
        
        string trackArtist = IsVariousArtists(track.Artist) || !string.IsNullOrWhiteSpace(bestMatchedArtist?.Name) 
                                ? bestMatchedArtist.Name : track.Artist;

        if (string.IsNullOrWhiteSpace(trackArtist))
        {
            Console.WriteLine("Unable to search for a track without the artist name on MusicBrainz");
            return result;
        }
        
        var searchResult = await _musicBrainzAPIService.SearchReleaseAsync(trackArtist, track.Album, track.Title);
        
        if (searchResult?.Recordings?.Count == 0)
        {
            //try without the artist name in the title for a match
            string titleWithoutArtistName = track.Title.ToLower().Replace(trackArtist.ToLower(), string.Empty);
            string albumWithoutArtistName = track.Album.ToLower().Replace(trackArtist.ToLower(), string.Empty);

            if (!string.IsNullOrWhiteSpace(bestMatchedArtist?.JoinPhrase))
            {
                titleWithoutArtistName = CleanupArtistCredit(titleWithoutArtistName, bestMatchedArtist.JoinPhrase);
            }
            
            if (titleWithoutArtistName.Length >= MinimumArtistName && 
                albumWithoutArtistName.Length >= MinimumArtistName)
            {
                searchResult = await _musicBrainzAPIService.SearchReleaseAsync(track.Artist, albumWithoutArtistName, titleWithoutArtistName);
            }
        }
        
        foreach (var recording in searchResult?.Recordings ?? [])
        {
            var tempRelease = GetBestMatchingRecordingRelease(recording, track, trackArtist);
           
            if (tempRelease == null)
            {
                continue;
            }

            result.RecordingQuery = recording;
            var matchedArtist = GetBestMatchingArtist(tempRelease.ArtistCredit, track);
            if (matchedArtist != null)
            {
                bestMatchedArtist = matchedArtist;
                
                result.ArtistCountry = !string.IsNullOrWhiteSpace(bestMatchedArtist?.Artist?.Id) ? 
                    (await _musicBrainzAPIService.GetArtistInfoAsync(bestMatchedArtist.Artist.Id))?.Country 
                    : string.Empty;
            }
            
            var mbTempRelease = await _musicBrainzAPIService.GetReleaseWithAllAsync(tempRelease.Id);
            if (mbTempRelease != null)
            {
                MusicBrainzArtistModel musicBrainzArtistModel = new MusicBrainzArtistModel();
                musicBrainzArtistModel.Releases.Add(mbTempRelease);

                if (bestMatchedArtist != null)
                {
                    musicBrainzArtistModel.ArtistCredit.AddRange(recording.ArtistCredit);
                }
                
                var release = GetBestMatchingRelease(musicBrainzArtistModel, track, result.ArtistCountry, trackArtist, relaxedFiltering);

                if (release != null)
                {
                    result.MatchedRelease = release;
                    result.Success = true;
                    return result;
                }
            }
        }
        
        return result;
    }

    private async Task<GetDataByAcoustIdResult> GetDataByAcoustIdAsync(FpcalcOutput? fingerprint, 
        MediaFileInfo mediaFileInfo, 
        FileInfo fromFile, 
        string acoustIdApiKey)
    {
        if (fingerprint is null)
        {
            Console.WriteLine("Failed to generate fingerprint, corrupt file?");
        }

        string? recordingId = string.Empty;
        string? acoustId = string.Empty;
        AcoustIdRecordingResponse? matchedRecording = null;
        
        if (!string.IsNullOrWhiteSpace(mediaFileInfo.AcoustId))
        {
            Console.WriteLine($"Looking up AcoustID provided by AcoustId Tag, '{fromFile.FullName}'");
            
            //try again but with the AcoustID from the media file
            var acoustIdLookup = await _acoustIdService.LookupByAcoustIdAsync(acoustIdApiKey, mediaFileInfo.AcoustId);
            matchedRecording = GetBestMatchingAcoustId(acoustIdLookup, mediaFileInfo, ref acoustId);
            recordingId = matchedRecording?.Id;
            
            if (!string.IsNullOrWhiteSpace(recordingId) && !string.IsNullOrWhiteSpace(acoustId))
            {
                Console.WriteLine($"Found AcoustId info from the AcoustId service by the AcoustID provided by the media Tag, '{fromFile.FullName}'");
            }
        }

        if (string.IsNullOrWhiteSpace(recordingId) && !string.IsNullOrWhiteSpace(fingerprint?.Fingerprint) && fingerprint?.Duration > 0)
        {
            var acoustIdLookup = await _acoustIdService.LookupAcoustIdAsync(acoustIdApiKey, fingerprint.Fingerprint, (int)fingerprint.Duration);
            matchedRecording = GetBestMatchingAcoustId(acoustIdLookup, mediaFileInfo, ref acoustId);
            recordingId = matchedRecording?.Id;

            if (!string.IsNullOrWhiteSpace(mediaFileInfo.AcoustId) && 
                (string.IsNullOrWhiteSpace(recordingId) || string.IsNullOrWhiteSpace(acoustId)))
            {
                Console.WriteLine($"Looking up AcoustID provided by AcoustId Tag, '{fromFile.FullName}'");
            
                //try again but with the AcoustID from the media file
                acoustIdLookup = await _acoustIdService.LookupByAcoustIdAsync(acoustIdApiKey, mediaFileInfo.AcoustId);
                matchedRecording = GetBestMatchingAcoustId(acoustIdLookup, mediaFileInfo, ref acoustId);
                recordingId = matchedRecording?.Id;
            
                if (!string.IsNullOrWhiteSpace(recordingId) && !string.IsNullOrWhiteSpace(acoustId))
                {
                    Console.WriteLine($"Found AcoustId info from the AcoustId service by the AcoustID provided by the media Tag, '{fromFile.FullName}'");
                }
            }
        }
        
        GetDataByAcoustIdResult result = new GetDataByAcoustIdResult();
        
        if (string.IsNullOrWhiteSpace(recordingId) || string.IsNullOrWhiteSpace(acoustId))
        {
            Console.WriteLine($"MusicBrainz recording not found by id '{recordingId}'");
            result.Success = false;
        }
        else
        {
            result.RecordingId = recordingId;
            result.AcoustId = acoustId;
            result.MatchedRecording = matchedRecording;
            result.Success = true;
        }

        return result;
    }

    private bool IsVariousArtists(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }
        
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

    private string GetArtistFeatCreditString(List<MusicBrainzArtistCreditModel> artists, string? trackArtist, bool addBrackets)
    {
        string artistName = string.Empty;

        if (artists.Count > 1)
        {
            if (addBrackets)
            {
                artistName += "(";
            }

            if (!string.IsNullOrWhiteSpace(trackArtist))
            {
                artists = artists
                    .Where(artist => !string.Equals(artist.Name, trackArtist))
                    .ToList();
            }

            int index = 0;
            string joinPhrase = artists.FirstOrDefault()?.JoinPhrase ?? string.Empty;

            string[] replaceToFeat = new[] { ",", "&" };
            if (joinPhrase.Length > 0 && replaceToFeat.Any(feat => joinPhrase.Trim() == feat))
            {
                joinPhrase = "feat. ";
            }
            
            foreach (var artist in artists)
            {
                if (index == 0 && joinPhrase.StartsWith(' '))
                {
                    joinPhrase = joinPhrase.TrimStart();
                }
                
                artistName += $"{joinPhrase}{artist.Name}";
                joinPhrase = " & ";//artist.JoinPhrase ?? string.Empty;
                index++;
            }
            if (addBrackets)
            {
                artistName += ")";
            }
        }

        return artistName.Trim();
    }

    private string CleanupArtistCredit(string trackName, string? joinPhrase)
    {
        if (!string.IsNullOrWhiteSpace(joinPhrase) &&
            trackName.ToLower().Contains(joinPhrase.Trim().ToLower()))
        {
            int index = trackName.ToLower().IndexOf(joinPhrase.Trim().ToLower());
            if (index > 1)
            {
                trackName = trackName.ToLower().Substring(0, index);
            }
        }
            
        //cleanup some characters that might fail the search
        trackName = trackName
            .Replace("[", string.Empty)
            .Replace("]", string.Empty)
            .Replace("(", string.Empty)
            .Replace(")", string.Empty)
            .Replace(":", string.Empty)
            .Trim();

        return trackName;
    }
}