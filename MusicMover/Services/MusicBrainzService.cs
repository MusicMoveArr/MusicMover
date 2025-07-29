using System.Globalization;
using ATL;
using FuzzySharp;
using MusicMover.Helpers;
using MusicMover.Models;
using MusicMover.Models.MusicBrainz;

namespace MusicMover.Services;

public class MusicBrainzService
{
    private const string VariousArtists = "Various Artists";
    private const string VariousArtistsVa = "VA";
    private readonly MusicBrainzAPIService _musicBrainzApiService;
    private readonly AcoustIdService _acoustIdService;
    private readonly FingerPrintService _fingerPrintService;
    private readonly MediaTagWriteService _mediaTagWriteService;
    private const int MinimumArtistName = 2; //prevents very short, non-artist names for example to be used for searching/matching
    private const int ArtistMatchPercentage = 80;
    
    private string[] IgnoreNames =
    [
        "[unknown]",
        "[anonymous]",
        "[traditional]",
        "[no artist]"
    ];
    
    public MusicBrainzService()
    {
        _musicBrainzApiService = new MusicBrainzAPIService();
        _acoustIdService = new AcoustIdService();
        _fingerPrintService = new FingerPrintService();
        _mediaTagWriteService = new MediaTagWriteService();
    }
    
    public async Task<bool> WriteTagFromAcoustIdAsync(
        AcoustIdResultMatch match,
        MediaFileInfo mediaFileInfo, 
        bool overWriteArtist, 
        bool overWriteAlbum, 
        bool overWriteTrack, 
        bool overwriteAlbumArtist)
    {
        if (match.ReleaseMedia == null || match.Release == null)
        {
            return false;
        }

        string? ignoreName = IgnoreNames.FirstOrDefault(ignoreName => ignoreName.Equals(match.ArtistCredit?.Name, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(match.ArtistCredit?.Name) &&
            !string.IsNullOrWhiteSpace(ignoreName))
        {
            Logger.WriteLine($"Artistname from MusicBrainz contained '{ignoreName}', skipped tagging");
            return false;
        }
        
        bool trackInfoUpdated = false;
        
        string? musicBrainzReleaseArtistId = match.ArtistCredit?.Artist?.Id;
        string? musicBrainzAlbumId = match.Release.Id;
        string? musicBrainzReleaseGroupId = match.Release.ReleaseGroup?.Id;
        
        string artists = string.Join(';', match.ArtistCredits.Select(artist => artist.Name));
        string musicBrainzArtistIds = string.Join(';', match.ArtistCredits
            .Where(artist => !string.IsNullOrWhiteSpace(artist.Artist?.Id))
            .Select(artist => artist.Artist?.Id));
        
        string isrcs = string.Join(';', match.ISRCS);

        if (!string.IsNullOrWhiteSpace(match.Release.Id))
        {
            var withLabelInfo = await _musicBrainzApiService.GetReleaseWithLabelAsync(match.Release.Id);
            var label = withLabelInfo?.LabeLInfo?.FirstOrDefault(label => label?.Label?.Type?.Contains("production", StringComparison.OrdinalIgnoreCase) == true);

            if (label == null && withLabelInfo?.LabeLInfo?.Count == 1)
            {
                label = withLabelInfo?.LabeLInfo?.FirstOrDefault();
            }
            if (!string.IsNullOrWhiteSpace(label?.Label?.Name))
            {
                _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "LABEL", label?.Label.Name, ref trackInfoUpdated);
                _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "CATALOGNUMBER", label?.CataLogNumber, ref trackInfoUpdated);
            }
        }
        
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "date", match.Release.Date, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "originaldate", match.Release.Date, ref trackInfoUpdated);

        if (string.IsNullOrWhiteSpace(mediaFileInfo.Title) || overWriteTrack)
        {
            string? trackTitle = match.ReleaseMedia.Tracks?.FirstOrDefault()?.Title;
            string credits = GetArtistFeatCreditString(match.ArtistCredits, match.ArtistCredit?.Name, true);
            if (credits.Length > 2)
            {
                trackTitle += " " + credits;
            }
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Title", trackTitle, ref trackInfoUpdated);
        }
        if (overWriteAlbum && 
            !string.IsNullOrWhiteSpace(match.Release.Title) && 
            !string.IsNullOrWhiteSpace(mediaFileInfo.Album))
        {
            if (!string.IsNullOrWhiteSpace(match.Release.Disambiguation) &&
                !match.Release.Title.ToLower().Contains(match.Release.Disambiguation.ToLower()) &&
                mediaFileInfo.Album.ToLower().Contains(match.Release.Disambiguation.ToLower()) &&
                !match.Release.Title.Trim().EndsWith(')'))
            {
                string disambiguation = string.Join(' ',
                    match.Release.Disambiguation
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(dis => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(dis)));
                match.Release.Title += $" ({disambiguation})";
            } 
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Album", match.Release.Title, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(mediaFileInfo.AlbumArtist)  || mediaFileInfo.AlbumArtist.ToLower().Contains("various") || overwriteAlbumArtist)
        {
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "AlbumArtist", match.ArtistCredit?.Name, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(mediaFileInfo.Artist) || mediaFileInfo.Artist.ToLower().Contains("various") || overWriteArtist)
        {
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Artist", match.ArtistCredit?.Name, ref trackInfoUpdated);
        }

        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "ARTISTS", artists, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "ISRC", isrcs, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "SCRIPT", match.Release.TextRepresentation?.Script, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "barcode", match.Release.Barcode, ref trackInfoUpdated);

        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Artist Id", musicBrainzArtistIds, ref trackInfoUpdated);

        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Track Id", match.RecordingId, ref trackInfoUpdated);
        
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Release Track Id", match.RecordingId, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Release Artist Id", musicBrainzReleaseArtistId, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Release Group Id", musicBrainzReleaseGroupId, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Release Id", match.Release.Id, ref trackInfoUpdated);

        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Album Artist Id", musicBrainzArtistIds, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Album Id", musicBrainzAlbumId, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Album Type", match.Release.ReleaseGroup?.PrimaryType, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Album Release Country", match.Release.Country, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Album Status", match.Release.Status, ref trackInfoUpdated);

        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Acoustid Id", match.AcoustId, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Acoustid Fingerprint", match.Fingerprint?.Fingerprint, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Acoustid Fingerprint Duration", match.Fingerprint?.Duration.ToString(CultureInfo.InvariantCulture), ref trackInfoUpdated);

        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Date", match.Release.ReleaseGroup?.FirstReleaseDate, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "originaldate", match.Release.ReleaseGroup?.FirstReleaseDate, ref trackInfoUpdated);
        
        if (match.Release.ReleaseGroup?.FirstReleaseDate?.Length >= 4)
        {
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "originalyear", match.Release.ReleaseGroup.FirstReleaseDate[..4], ref trackInfoUpdated);
        }
        
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Disc Number", match.ReleaseMedia.Position?.ToString(), ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Track Number", match.ReleaseMedia.Tracks?.FirstOrDefault()?.Position?.ToString(), ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Total Tracks", match.ReleaseMedia.TrackCount.ToString(), ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MEDIA", match.ReleaseMedia.Format, ref trackInfoUpdated);

        return await _mediaTagWriteService.SafeSaveAsync(mediaFileInfo.TrackInfo);
    }

    public async Task<AcoustIdResultMatch?> GetMatchFromAcoustIdAsync(
        MediaFileInfo mediaFileInfo,
        FpcalcOutput? fingerprint,
        string acoustIdApiKey,
        bool searchByTagNames,
        int acoustIdMatchPercentage,
        int musicBrainzMatchPercentage)
    {
        string? recordingId = string.Empty;
        string? acoustId = string.Empty;
        
        string artistCountry = string.Empty;
        MusicBrainzArtistCreditModel? artistCredit = null;
        MusicBrainzArtistReleaseModel? release = null;
        List<MusicBrainzArtistCreditModel> artistCredits = new List<MusicBrainzArtistCreditModel>();
        List<string>? listISRCs = new List<string>();
        GetDataByAcoustIdResult acoustIdResult = await GetDataByAcoustIdAsync(fingerprint, mediaFileInfo, acoustIdApiKey, acoustIdMatchPercentage);

        if (acoustIdResult.Success && !string.IsNullOrWhiteSpace(acoustIdResult.RecordingId))
        {
            var data = await _musicBrainzApiService.GetRecordingByIdAsync(acoustIdResult.RecordingId);
            if (data != null)
            {
                artistCredit = GetBestMatchingArtist(data.ArtistCredit, mediaFileInfo.TrackInfo);
            
                artistCountry = !string.IsNullOrWhiteSpace(artistCredit?.Artist?.Id) ? 
                    (await _musicBrainzApiService.GetArtistInfoAsync(artistCredit.Artist.Id))?.Country ?? string.Empty
                    : string.Empty;
        
                release = GetBestMatchingRelease(data, mediaFileInfo.TrackInfo, artistCountry, artistCredit?.Name, false, acoustIdMatchPercentage);

                if (release == null)
                {
                    data = await _musicBrainzApiService.GetRecordingByIdAsync(acoustIdResult.MatchedRecording.Id);
                    mediaFileInfo.TrackInfo.Title = acoustIdResult.MatchedRecording?.Title;
                    
                    release = GetBestMatchingRelease(data, mediaFileInfo.TrackInfo, artistCountry, artistCredit?.Name, true, acoustIdMatchPercentage);
                    mediaFileInfo.TrackInfo.Title = mediaFileInfo.Title;
                }
                
                artistCredits = data?.ArtistCredit ?? [];
                listISRCs = data?.ISRCS ?? [];
            }
        }

        if (release == null && searchByTagNames)
        {
            SearchBestMatchingReleaseResult matchingReleaseResult = await SearchBestMatchingReleaseAsync(mediaFileInfo.TrackInfo, artistCredit, false, musicBrainzMatchPercentage);

            if (!matchingReleaseResult.Success)
            {
                Logger.WriteLine($"MusicBrainz recording not found by id '{recordingId}' by searching from tag names, Artist: {mediaFileInfo.Artist}, ALbum: {mediaFileInfo.Album}, Title: {mediaFileInfo.Title}", true);
                return null;
            }

            artistCredit = GetBestMatchingArtist(matchingReleaseResult.RecordingQuery?.ArtistCredit, mediaFileInfo.TrackInfo);
            artistCredits = matchingReleaseResult.RecordingQuery?.ArtistCredit ?? [];
            listISRCs = matchingReleaseResult.RecordingQuery?.ISRCS ?? [];
            release = matchingReleaseResult.MatchedRelease;
        }

        MusicBrainzReleaseMediaModel? releaseMedia = release
            ?.Media
            ?.FirstOrDefault();

        return new AcoustIdResultMatch
        {
            ArtistCredit = artistCredit,
            ISRCS = listISRCs,
            ReleaseMedia = releaseMedia,
            AcoustId = acoustId,
            Release = release,
            ArtistCredits = artistCredits,
            Fingerprint = fingerprint,
            RecordingId = recordingId
        };
    }

    public async Task<AcoustIdRecordingResponse?> GetBestMatchingAcoustIdAsync(
        AcoustIdResponse? acoustIdResponse, 
        MediaFileInfo mediaFileInfo,
        int matchPercentage)
    {
        if (acoustIdResponse?.Results?.Count == 0)
        {
            return null;
        }

        var highestScoreResult = acoustIdResponse
            ?.Results
            ?.Where(result => result.Recordings?.Any() == true)
            .Where(result => result.Score >= (matchPercentage / 100F))
            .OrderByDescending(result => result.Score)
            .FirstOrDefault();

        if (highestScoreResult == null)
        {
            return null;
        }

        //perhaps not the best approach but sometimes...
        bool ignoreFilters = string.IsNullOrWhiteSpace(mediaFileInfo.Album) ||
                             string.IsNullOrWhiteSpace(mediaFileInfo.Artist) ||
                             string.IsNullOrWhiteSpace(mediaFileInfo.Title);

        var recordingReleases = highestScoreResult.Recordings
            .Select(async x => new
            {
                RecordingId = x.Id,
                Recording = await _musicBrainzApiService.GetRecordingByIdAsync(x.Id)
            })
            .Select(x => x.Result)
            .ToList();
        
        var results = highestScoreResult
            .Recordings
           ?.Select(result => new
           {
               Result = result,
               Releases = recordingReleases.FirstOrDefault(release => string.Equals(release.RecordingId, result.Id))
           })
            ?.Select(result => new
            {
                AlbumMatchedFor = result.Releases.Recording.Releases
                    .Where(album => FuzzyHelper.ExactNumberMatch(album.Title, mediaFileInfo.Album))
                    .Select(album => FuzzyHelper.FuzzTokenSortRatioToLower(album.Title, mediaFileInfo.Album))
                    .OrderByDescending(match => match)
                    .FirstOrDefault(),
                ArtistMatchedFor = result.Result.Artists?.Sum(artist => FuzzyHelper.FuzzTokenSortRatioToLower(artist.Name, mediaFileInfo.Artist)) ?? 0,
                TitleMatchedFor = FuzzyHelper.FuzzTokenSortRatioToLower(mediaFileInfo.Title, result.Result.Title),
                LengthMatch = Math.Abs(mediaFileInfo.Duration - result.Result.Duration ?? 100),
                Result = result
            })
            .Where(match => ignoreFilters || FuzzyHelper.ExactNumberMatch(mediaFileInfo.Title, match.Result.Result.Title))
            //.Where(match => ignoreFilters || match.ArtistMatchedFor >= matchPercentage)
            //.Where(match => ignoreFilters || match.TitleMatchedFor >= matchPercentage)
            .OrderByDescending(result => result.ArtistMatchedFor)
            .ThenByDescending(result => result.AlbumMatchedFor)
            .ThenByDescending(result => result.TitleMatchedFor)
            .ThenBy(result => result.LengthMatch)
            .Select(result => result.Result)
            .ToList();

        AcoustIdRecordingResponse? firstResult = results?.FirstOrDefault()?.Result;
        if (firstResult != null)
        {
            firstResult.AcoustId = highestScoreResult.Id;
        }
        return firstResult;
    }

    public MusicBrainzArtistCreditModel? GetBestMatchingArtist(
        List<MusicBrainzArtistCreditModel>? artists, 
        Track track)
    {
        string[] splitTrackArtists = !string.IsNullOrWhiteSpace(track.AlbumArtist) ? 
                                        track.AlbumArtist.Split(new[] { ',', ';', '&' }, StringSplitOptions.RemoveEmptyEntries) : [];

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
                    .Where(match => match.MatchedFor >= ArtistMatchPercentage)
                    .OrderByDescending(match => match.MatchedFor)
                    .Select(match => match.Artist)
                    .FirstOrDefault();

                if (foundArtist != null)
                {
                    return foundArtist;
                }
            }
        }

        var matchedArtists = artists
            ?.Where(artist => !string.Equals(artist.Name, VariousArtists, StringComparison.OrdinalIgnoreCase))
            .Select(artist => new
            {
                Artist = artist,
                MatchedFor = Math.Max(FuzzyHelper.FuzzRatioToLower(artist.Name, track.Artist), 
                    FuzzyHelper.FuzzRatioToLower(artist.Name, track.AlbumArtist))
            })
            .Where(match => match.MatchedFor >= ArtistMatchPercentage)
            .OrderByDescending(match => match.MatchedFor)
            .Select(match => match.Artist)
            .ToList();
        
        return matchedArtists
            ?.FirstOrDefault();
    }

    public MusicBrainzArtistReleaseModel? GetBestMatchingRelease(MusicBrainzArtistModel? data, 
        Track track, 
        string? artistCountry,
        string? targetArtist,
        bool relaxedFiltering,
        int matchPercentage)
    {
        if (data == null)
        {
            return null;
        }

        string trackAlbum = IsVariousArtists(track.Album) ? string.Empty : track.Album;
        string trackBarcode = _mediaTagWriteService.GetTagValue(track, "barcode");
        
        var matchedReleases =
            data.Releases
                ?.Where(release => release.Media.Count != 0)
                .Select(release => new
                {
                    Album = release.Title,
                    release.Barcode,
                    Release = release
                })
                .Where(release => !string.IsNullOrWhiteSpace(release.Album))
                //.Where(release => !string.IsNullOrWhiteSpace(release.Release.Country))
                .Select(release => new
                {
                    AlbumName = release.Album,
                    release.Release,
                    AlbumMatch = !string.IsNullOrWhiteSpace(trackAlbum) ? Math.Max(
                        relaxedFiltering ? FuzzyHelper.PartialTokenSortRatioToLower($"{release.Album} {release.Release.Disambiguation}", trackAlbum) : FuzzyHelper.FuzzRatioToLower($"{release.Album} {release.Release.Disambiguation}", trackAlbum), //search with added "deluxe edition" etc
                        relaxedFiltering ? FuzzyHelper.PartialTokenSortRatioToLower(release.Album, trackAlbum) : FuzzyHelper.FuzzRatioToLower(release.Album, trackAlbum)) : 100,
                    ArtistMatch = !string.IsNullOrWhiteSpace(targetArtist) ? data.ArtistCredit.Sum(artist => FuzzyHelper.FuzzRatioToLower(targetArtist, artist.Name)) : 100,
                    CountryMatch = !string.IsNullOrWhiteSpace(artistCountry) ? relaxedFiltering ? FuzzyHelper.PartialTokenSortRatioToLower(release.Release.Country, artistCountry) : FuzzyHelper.FuzzRatioToLower(release.Release.Country, artistCountry) : 0,
                    BarcodeMatch = !string.IsNullOrWhiteSpace(release.Barcode) ? relaxedFiltering ? FuzzyHelper.PartialTokenSortRatioToLower(release.Barcode, trackBarcode) : FuzzyHelper.FuzzRatioToLower(release.Barcode, trackBarcode) : 0
                })
                //.Where(match => relaxedFiltering || FuzzyHelper.ExactNumberMatch(trackAlbum, match.AlbumName))
                .OrderByDescending(match => match.AlbumMatch)
                .ThenByDescending(match => match.CountryMatch)
                .ThenByDescending(match => match.BarcodeMatch)
                .ToList();
        
        var potentialReleases = matchedReleases
            ?.Where(match => match.AlbumMatch >= matchPercentage)
            ?.Where(match => match.ArtistMatch >= matchPercentage)
            ?.ToList() ?? [];

        foreach (var potentialRelease in potentialReleases)
        {
            foreach (var media in potentialRelease.Release.Media)
            {
                var tempTracks = media.Tracks;
                media.Tracks = GetBestMatchingTracks(tempTracks, track.Title, string.Empty, relaxedFiltering, matchPercentage);

                if (media?.Tracks?.Count == 0 && 
                    !string.IsNullOrWhiteSpace(targetArtist) &&
                    track.Title.ToLower().Contains(targetArtist.ToLower()))
                {
                    //try without the artist name in the title for a match
                    string withoutArtistName = track.Title.ToLower().Replace(targetArtist.ToLower(), string.Empty);
                    if (withoutArtistName.Length >= MinimumArtistName)
                    {
                        media.Tracks = GetBestMatchingTracks(tempTracks, withoutArtistName, string.Empty, relaxedFiltering, matchPercentage);
                    }
                }

                if (media?.Tracks?.Count == 0 && data?.ArtistCredit?.Count > 1)
                {
                    //try by adding artist credit join phrase
                    string artistCredits = GetArtistFeatCreditString(data.ArtistCredit, string.Empty, true);
                    media.Tracks = GetBestMatchingTracks(tempTracks, track.Title, artistCredits, relaxedFiltering, matchPercentage);
                }

                if (media?.Tracks?.Count == 0 &&
                    data?.ArtistCredit?.Count > 1 &&
                    !string.IsNullOrWhiteSpace(data.ArtistCredit.First().JoinPhrase))
                {
                    //try by removing the join phrase
                    string titleWithoutCredit = CleanupArtistCredit(track.Title, data?.ArtistCredit?.First()?.JoinPhrase);
                    
                    if (!string.IsNullOrWhiteSpace(titleWithoutCredit))
                    {
                        media.Tracks = GetBestMatchingTracks(tempTracks, titleWithoutCredit, string.Empty, relaxedFiltering, matchPercentage);
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

    public List<MusicBrainzReleaseMediaTrackModel>? GetBestMatchingTracks(
        List<MusicBrainzReleaseMediaTrackModel>? tracks, 
        string trackTitle, 
        string artistCredit, 
        bool relaxedFiltering,
        int matchPercentage)
    {
        var matches = tracks?
            .Select(releaseTrack => new
            {
                Track = releaseTrack,
                Length = releaseTrack.Length / 1000 ?? int.MaxValue,
                TitleMatch = relaxedFiltering 
                    ? FuzzyHelper.PartialTokenSortRatioToLower(trackTitle, releaseTrack.Title + artistCredit)
                    : FuzzyHelper.FuzzRatioToLower(trackTitle, releaseTrack.Title + artistCredit)
            })
            //.Where(match => match.Track.Recording != null)
            .OrderByDescending(releaseTrack => releaseTrack.TitleMatch)
            .ThenBy(releaseTrack => releaseTrack.Length)
            .ToList();

        return matches
            .Where(match => match.TitleMatch >= matchPercentage)
            .Where(match => relaxedFiltering || FuzzyHelper.ExactNumberMatch(trackTitle, match.Track.Title))
            .Select(releaseTrack => releaseTrack.Track)
            .ToList();;
    }

    public MusicBrainzRecordingQueryReleaseEntityModel? GetBestMatchingRecordingRelease(
        MusicBrainzRecordingQueryEntityModel? data, 
        Track track,
        int matchPercentage,
        ref string trackArtist)
    {
        if (data == null)
        {
            return null;
        }

        List<string> artistNames = new List<string>();
        artistNames.Add(trackArtist);
        artistNames.Add(track.Artist);
        artistNames.Add(track.AlbumArtist);
        artistNames.Add(ArtistHelper.GetUncoupledArtistName(trackArtist));
        artistNames.Add(ArtistHelper.GetUncoupledArtistName(track.Artist));
        artistNames.Add(ArtistHelper.GetUncoupledArtistName(track.AlbumArtist));

        foreach (string artistName in artistNames.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct())
        {
            string trackAlbumWithoutArtist = track.Album.Contains(artistName, StringComparison.OrdinalIgnoreCase) ? 
                track.Album.ToLower().Replace(artistName.ToLower(), string.Empty) 
                : track.Album;
            
            var matchedReleases =
                data?.Releases
                    ?.Where(release => data.ArtistCredit?.Count > 0)
                    ?.Where(release => release.Media.Count > 0)
                    .Select(release => new
                    {
                        Album = release.Title,
                        Release = release
                    })
                    .Where(release => !string.IsNullOrWhiteSpace(release.Album))
                    //.Where(release => !string.IsNullOrWhiteSpace(release.Release.Country))
                    .Select(release => new
                    {
                        AlbumName = release.Album,
                        release.Release,
                        ArtistMatch = data.ArtistCredit.Sum(artist => FuzzyHelper.FuzzRatioToLower(artistName, artist.Name)),
                        AlbumMatch = !string.IsNullOrWhiteSpace(track.Album) ? Math.Max(FuzzyHelper.FuzzRatioToLower(release.Album, track.Album), 
                            FuzzyHelper.FuzzRatioToLower(release.Album, trackAlbumWithoutArtist)): 100
                    })
                    .Where(match => FuzzyHelper.ExactNumberMatch(track.Album, match.AlbumName))
                    .OrderByDescending(match => match.ArtistMatch)
                    .ThenByDescending(match => match.AlbumMatch)
                    .ToList();

            var matchedRelease = matchedReleases
                ?.Where(match => match.AlbumMatch >= matchPercentage)
                .Where(match => match.ArtistMatch >= matchPercentage)
                .Select(match => match.Release)
                .FirstOrDefault();

            if (matchedRelease != null)
            {
                trackArtist = artistName;
                return matchedRelease;
            }
        }

        return null;
    }

    public async Task<SearchBestMatchingReleaseResult> SearchBestMatchingReleaseAsync(Track track,
        MusicBrainzArtistCreditModel? bestMatchedArtist,
        bool relaxedFiltering,
        int matchPercentage)
    {
        var result = new SearchBestMatchingReleaseResult();
        result.BestMatchedArtist = bestMatchedArtist;
        
        string trackArtist = IsVariousArtists(track.Artist) && !string.IsNullOrWhiteSpace(bestMatchedArtist?.Name) 
                                ? bestMatchedArtist.Name : track.Artist;
        //trackArtist = ArtistHelper.GetUncoupledArtistName(trackArtist);

        if (string.IsNullOrWhiteSpace(trackArtist))
        {
            Logger.WriteLine("Unable to search for a track without the artist name on MusicBrainz", true);
            return result;
        }
        
        var searchResult = await _musicBrainzApiService.SearchReleaseAsync(trackArtist, track.Album, track.Title);
        
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
                searchResult = await _musicBrainzApiService.SearchReleaseAsync(track.Artist, albumWithoutArtistName, titleWithoutArtistName);
            }
        }
        
        foreach (var recording in searchResult?.Recordings ?? [])
        {
            var tempRelease = GetBestMatchingRecordingRelease(recording, track, matchPercentage, ref trackArtist);
           
            if (tempRelease == null)
            {
                continue;
            }

            result.RecordingQuery = recording;
            var matchedArtist = GetBestMatchingArtist(tempRelease.ArtistCredit, track);

            if (matchedArtist == null)
            {
                matchedArtist = GetBestMatchingArtist(recording.ArtistCredit, track);
            }
            
            if (matchedArtist != null)
            {
                bestMatchedArtist = matchedArtist;
                
                result.ArtistCountry = !string.IsNullOrWhiteSpace(bestMatchedArtist?.Artist?.Id) ? 
                    (await _musicBrainzApiService.GetArtistInfoAsync(bestMatchedArtist.Artist.Id))?.Country 
                    : string.Empty;
            }
            
            var mbTempRelease = await _musicBrainzApiService.GetReleaseWithAllAsync(tempRelease.Id);
            if (mbTempRelease != null)
            {
                MusicBrainzArtistModel musicBrainzArtistModel = new MusicBrainzArtistModel();
                musicBrainzArtistModel.Releases.Add(mbTempRelease);

                if (bestMatchedArtist != null)
                {
                    musicBrainzArtistModel.ArtistCredit.AddRange(recording.ArtistCredit);
                }
                
                var release = GetBestMatchingRelease(musicBrainzArtistModel, track, result.ArtistCountry, trackArtist, relaxedFiltering, matchPercentage);

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

    public async Task<GetDataByAcoustIdResult> GetDataByAcoustIdAsync(FpcalcOutput? fingerprint, 
        MediaFileInfo mediaFileInfo, 
        string acoustIdApiKey,
        int matchPercentage)
    {
        string? recordingId = string.Empty;
        string? acoustId = string.Empty;
        AcoustIdRecordingResponse? matchedRecording = null;
        
        if (!string.IsNullOrWhiteSpace(mediaFileInfo.AcoustId))
        {
            Logger.WriteLine($"Looking up AcoustID provided by AcoustId Tag", true);
            
            //try again but with the AcoustID from the media file
            var acoustIdLookup = await _acoustIdService.LookupByAcoustIdAsync(acoustIdApiKey, mediaFileInfo.AcoustId);
            matchedRecording = await GetBestMatchingAcoustIdAsync(acoustIdLookup, mediaFileInfo, matchPercentage);
            acoustId = matchedRecording?.AcoustId;
            recordingId = matchedRecording?.Id;
            
            if (!string.IsNullOrWhiteSpace(recordingId) && !string.IsNullOrWhiteSpace(acoustId))
            {
                Logger.WriteLine($"Found AcoustId info from the AcoustId service by the AcoustID provided by the media Tag", true);
            }
        }

        if (string.IsNullOrWhiteSpace(recordingId) && !string.IsNullOrWhiteSpace(fingerprint?.Fingerprint) && fingerprint?.Duration > 0)
        {
            var acoustIdLookup = await _acoustIdService.LookupAcoustIdAsync(acoustIdApiKey, fingerprint.Fingerprint, (int)fingerprint.Duration);
            matchedRecording = await GetBestMatchingAcoustIdAsync(acoustIdLookup, mediaFileInfo, matchPercentage);
            acoustId = matchedRecording?.AcoustId;
            recordingId = matchedRecording?.Id;

            if (!string.IsNullOrWhiteSpace(mediaFileInfo.AcoustId) && 
                (string.IsNullOrWhiteSpace(recordingId) || string.IsNullOrWhiteSpace(acoustId)))
            {
                Logger.WriteLine($"Looking up AcoustID provided by AcoustId Tag", true);
            
                //try again but with the AcoustID from the media file
                acoustIdLookup = await _acoustIdService.LookupByAcoustIdAsync(acoustIdApiKey, mediaFileInfo.AcoustId);
                matchedRecording = await GetBestMatchingAcoustIdAsync(acoustIdLookup, mediaFileInfo, matchPercentage);
                acoustId = matchedRecording?.AcoustId;
                recordingId = matchedRecording?.Id;
            
                if (!string.IsNullOrWhiteSpace(recordingId) && !string.IsNullOrWhiteSpace(acoustId))
                {
                    Logger.WriteLine($"Found AcoustId info from the AcoustId service by the AcoustID provided by the media Tag", true);
                }
            }
        }
        
        GetDataByAcoustIdResult result = new GetDataByAcoustIdResult();
        
        if (string.IsNullOrWhiteSpace(recordingId) || string.IsNullOrWhiteSpace(acoustId))
        {
            Logger.WriteLine($"MusicBrainz recording not found by id '{recordingId}'", true);
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

    public bool IsVariousArtists(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }
        
        if (string.Equals(name, VariousArtistsVa, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (FuzzyHelper.FuzzRatioToLower(name, VariousArtists) >= 95)
        {
            return true;
        }

        return false;
    }

    public string GetArtistFeatCreditString(List<MusicBrainzArtistCreditModel> artists, string? trackArtist, bool addBrackets)
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

            string[] replaceToFeat = [ ",", "&" ];
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

    public string CleanupArtistCredit(string trackName, string? joinPhrase)
    {
        if (!string.IsNullOrWhiteSpace(joinPhrase) &&
            trackName.ToLower().Contains(joinPhrase.Trim().ToLower()))
        {
            int index = trackName.IndexOf(joinPhrase.Trim(), StringComparison.OrdinalIgnoreCase);
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