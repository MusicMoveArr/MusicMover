using System.Globalization;
using FuzzySharp;
using MusicMover.Helpers;
using MusicMover.MediaHandlers;
using MusicMover.Models.AcoustId;
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
        MediaHandler mediaHandler, 
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
                _mediaTagWriteService.UpdateTag(mediaHandler, "LABEL", label?.Label.Name, ref trackInfoUpdated);
                _mediaTagWriteService.UpdateTag(mediaHandler, "CATALOGNUMBER", label?.CataLogNumber, ref trackInfoUpdated);
            }
        }
        
        _mediaTagWriteService.UpdateTag(mediaHandler, "date", match.Release.Date, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "originaldate", match.Release.Date, ref trackInfoUpdated);

        if (string.IsNullOrWhiteSpace(mediaHandler.Title) || overWriteTrack)
        {
            string? trackTitle = match.ReleaseMedia.Tracks?.FirstOrDefault()?.Title;
            //string credits = GetArtistFeatCreditString(match.ArtistCredits);
            //if (credits.Length > 2)
            //{
            //    trackTitle += " " + credits;
            //}
            _mediaTagWriteService.UpdateTag(mediaHandler, "Title", trackTitle, ref trackInfoUpdated);
        }
        if (overWriteAlbum && 
            !string.IsNullOrWhiteSpace(match.Release.Title))
        {
            if (!string.IsNullOrWhiteSpace(match.Release.Disambiguation) &&
                !match.Release.Title.ToLower().Contains(match.Release.Disambiguation.ToLower()) &&
                !match.Release.Title.Trim().EndsWith(')'))
            {
                string disambiguation = string.Join(' ',
                    match.Release.Disambiguation
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(dis => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(dis)));
                match.Release.Title += $" ({disambiguation})";
            } 
            _mediaTagWriteService.UpdateTag(mediaHandler, "Album", match.Release.Title, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(mediaHandler.AlbumArtist)  || mediaHandler.AlbumArtist.ToLower().Contains("various") || overwriteAlbumArtist)
        {
            _mediaTagWriteService.UpdateTag(mediaHandler, "AlbumArtist", match.ArtistCredit?.Name, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(mediaHandler.Artist) || mediaHandler.Artist.ToLower().Contains("various") || overWriteArtist)
        {
            _mediaTagWriteService.UpdateTag(mediaHandler, "Artist", match.ArtistCredit?.Name, ref trackInfoUpdated);
        }

        _mediaTagWriteService.UpdateTag(mediaHandler, "ARTISTS", artists, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "ISRC", isrcs, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "SCRIPT", match.Release.TextRepresentation?.Script, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "barcode", match.Release.Barcode, ref trackInfoUpdated);

        _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Artist Id", musicBrainzArtistIds, ref trackInfoUpdated);

        _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Track Id", match.RecordingId, ref trackInfoUpdated);
        
        _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Release Track Id", match.RecordingId, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Release Artist Id", musicBrainzReleaseArtistId, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Release Group Id", musicBrainzReleaseGroupId, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Release Id", match.Release.Id, ref trackInfoUpdated);

        _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Album Artist Id", musicBrainzArtistIds, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Album Id", musicBrainzAlbumId, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Album Type", match.Release.ReleaseGroup?.PrimaryType, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Album Release Country", match.Release.Country, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Album Status", match.Release.Status, ref trackInfoUpdated);

        _mediaTagWriteService.UpdateTag(mediaHandler, "Acoustid Id", match.AcoustId, ref trackInfoUpdated);

        _mediaTagWriteService.UpdateTag(mediaHandler, "Date", match.Release.ReleaseGroup?.FirstReleaseDate, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "originaldate", match.Release.ReleaseGroup?.FirstReleaseDate, ref trackInfoUpdated);
        
        if (match.Release.ReleaseGroup?.FirstReleaseDate?.Length >= 4)
        {
            _mediaTagWriteService.UpdateTag(mediaHandler, "originalyear", match.Release.ReleaseGroup.FirstReleaseDate[..4], ref trackInfoUpdated);
        }
        
        _mediaTagWriteService.UpdateTag(mediaHandler, "Disc Number", match.ReleaseMedia.Position?.ToString(), ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "Track Number", match.ReleaseMedia.Tracks?.FirstOrDefault()?.Position?.ToString(), ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "Total Tracks", match.ReleaseMedia.TrackCount.ToString(), ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "MEDIA", match.ReleaseMedia.Format, ref trackInfoUpdated);

        if (trackInfoUpdated)
        {
            mediaHandler.TaggerUpdatedTags = true;
        }
        return true;
    }

    public async Task<AcoustIdResultMatch?> GetMatchFromAcoustIdAsync(
        MediaHandler mediaHandler,
        string acoustIdApiKey,
        bool searchByTagNames,
        int acoustIdMatchPercentage,
        int musicBrainzMatchPercentage,
        TimeSpan acoustIdMaxTimeSpan)
    {
        string artistCountry = string.Empty;
        MusicBrainzArtistCreditModel? artistCredit = null;
        MusicBrainzArtistReleaseModel? release = null;
        List<MusicBrainzArtistCreditModel> artistCredits = new List<MusicBrainzArtistCreditModel>();
        List<string>? listISRCs = new List<string>();
        GetDataByAcoustIdResult acoustIdResult = await GetDataByAcoustIdAsync(mediaHandler, acoustIdApiKey, acoustIdMatchPercentage, acoustIdMaxTimeSpan);

        if (acoustIdResult.Success && 
            !string.IsNullOrWhiteSpace(acoustIdResult.RecordingId))
        {
            var data = await _musicBrainzApiService.GetRecordingByIdAsync(acoustIdResult.RecordingId);
            if (data != null)
            {
                artistCredit = GetBestMatchingArtist(data.ArtistCredit, mediaHandler);
            
                artistCountry = !string.IsNullOrWhiteSpace(artistCredit?.Artist?.Id) ? 
                    (await _musicBrainzApiService.GetArtistInfoAsync(artistCredit.Artist.Id))?.Country ?? string.Empty
                    : string.Empty;
        
                release = GetBestMatchingRelease(data, mediaHandler, artistCountry, artistCredit?.Name, false, acoustIdMatchPercentage);

                if (release == null)
                {
                    data = await _musicBrainzApiService.GetRecordingByIdAsync(acoustIdResult.MatchedRecording.Id);
                    if (!string.IsNullOrWhiteSpace(acoustIdResult.MatchedRecording?.Title))
                    {
                        mediaHandler.SetMediaTagValue(acoustIdResult.MatchedRecording?.Title, "Title");
                    }
                    
                    release = GetBestMatchingRelease(data, mediaHandler, artistCountry, artistCredit?.Name, true, acoustIdMatchPercentage);
                    if (!string.IsNullOrWhiteSpace(mediaHandler.Title))
                    {
                        mediaHandler.SetMediaTagValue(mediaHandler.Title, "Title");
                    }
                }
                
                artistCredits = data?.ArtistCredit ?? [];
                listISRCs = data?.ISRCS ?? [];
            }
        }

        if (release == null && searchByTagNames)
        {
            SearchBestMatchingReleaseResult matchingReleaseResult = await SearchBestMatchingReleaseAsync(mediaHandler, artistCredit, false, musicBrainzMatchPercentage);

            if (!matchingReleaseResult.Success)
            {
                Logger.WriteLine($"MusicBrainz recording not found by id '{acoustIdResult.RecordingId}' by searching from tag names, Artist: {mediaHandler.Artist}, ALbum: {mediaHandler.Album}, Title: {mediaHandler.Title}", true);
                return null;
            }

            artistCredit = GetBestMatchingArtist(matchingReleaseResult.RecordingQuery?.ArtistCredit, mediaHandler);
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
            AcoustId = acoustIdResult.AcoustId,
            Release = release,
            ArtistCredits = artistCredits,
            RecordingId = acoustIdResult.RecordingId
        };
    }

    public async Task<AcoustIdRecording?> GetBestMatchingAcoustIdAsync(
        AcoustIdResponse? acoustIdResponse, 
        MediaHandler mediaHandler,
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
        bool ignoreFilters = string.IsNullOrWhiteSpace(mediaHandler.Album) ||
                             !mediaHandler.AllArtistNames.Any() ||
                             string.IsNullOrWhiteSpace(mediaHandler.Title);

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
                    .Where(release => ignoreFilters || FuzzyHelper.ExactNumberMatch(release.Title, mediaHandler.Album))
                    .Select(release => new
                    {
                        MatchedFor = FuzzyHelper.FuzzTokenSortRatioToLower(release.Title, mediaHandler.Album),
                        Release = release
                    })
                    .OrderByDescending(match => match.MatchedFor)
                    .FirstOrDefault(),
                ArtistMatchedFor = mediaHandler.AllArtistNames.Count > 0 ?
                    result.Result.Artists?
                    .Sum(artist =>
                        mediaHandler.AllArtistNames.Max(mediaArtist => FuzzyHelper.FuzzTokenSortRatioToLower(artist.Name, mediaArtist))) ?? 0 : 0,
                
                TitleMatchedFor = FuzzyHelper.FuzzTokenSortRatioToLower(mediaHandler.Title, result.Result.Title),
                LengthMatch = Math.Abs(mediaHandler.Duration - result.Result.Duration ?? 100),
                AcoustIdResult = result
            })
            .Where(match => ignoreFilters || FuzzyHelper.ExactNumberMatch(mediaHandler.Title, match.AcoustIdResult.Result.Title))
            .Where(match => ignoreFilters || match.ArtistMatchedFor >= matchPercentage)
            .Where(match => ignoreFilters || match.TitleMatchedFor >= matchPercentage)
            .OrderByDescending(result => result.ArtistMatchedFor)
            .ThenByDescending(result => result.AlbumMatchedFor?.MatchedFor)
            .ThenByDescending(result => result.TitleMatchedFor)
            .ThenBy(result => result.LengthMatch)
            .Select(result => result)
            .ToList();

        var bestResult = results.FirstOrDefault();
        AcoustIdRecording? firstResult = bestResult?.AcoustIdResult.Result;
        if (firstResult != null)
        {
            firstResult.RecordingRelease = bestResult.AlbumMatchedFor?.Release;
            firstResult.AcoustId = highestScoreResult.Id;
        }
        return firstResult;
    }

    public MusicBrainzArtistCreditModel? GetBestMatchingArtist(
        List<MusicBrainzArtistCreditModel>? artists, 
        MediaHandler mediaHandler)
    {
        if (mediaHandler.AllArtistNames.Any())
        {
            foreach (string splitArtist in mediaHandler.AllArtistNames)
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
        
        //searching in normal tags like Artist/AlbumArtist first
        //searching as well in Title, it happens that people tag Artist/AlbumArtist differently with their website, collection or w\e

        string featArtist = GetArtistCreditString(artists);

        if (FuzzyHelper.FuzzRatioToLower(featArtist, mediaHandler.Artist) >= ArtistMatchPercentage ||
            FuzzyHelper.FuzzRatioToLower(featArtist, mediaHandler.AlbumArtist) >= ArtistMatchPercentage)
        {
            return artists.FirstOrDefault();
        }

        var matchedArtists = artists
            ?.Where(artist => !string.Equals(artist.Name, VariousArtists, StringComparison.OrdinalIgnoreCase))
            .Select(artist => new
            {
                Artist = artist,
                MatchedFor = Enumerable.Max<int>([
                             FuzzyHelper.FuzzRatioToLower(artist.Name, mediaHandler.Artist), //check in normal Artist tags
                             FuzzyHelper.FuzzRatioToLower(artist.Name, mediaHandler.AlbumArtist),
                             FuzzyHelper.PartialRatioToLower(artist.Name, mediaHandler.Title),//maybe artist name is in the title ?
                             
                ]) 
            })
            .OrderByDescending(match => match.MatchedFor)
            .ToList();
        
        return matchedArtists
            ?.Where(match => match.MatchedFor >= ArtistMatchPercentage || string.IsNullOrWhiteSpace(mediaHandler.Artist))
            ?.Select(match => match.Artist)
            ?.FirstOrDefault();
    }

    public MusicBrainzArtistReleaseModel? GetBestMatchingRelease(
        MusicBrainzArtistModel? data, 
        MediaHandler mediaHandler, 
        string? artistCountry,
        string? targetArtist,
        bool relaxedFiltering,
        int matchPercentage)
    {
        if (data == null)
        {
            return null;
        }

        string trackAlbum = IsVariousArtists(mediaHandler.Album) ? string.Empty : mediaHandler.Album;
        string? trackBarcode = mediaHandler.GetMediaTagValue("barcode");
        
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
                .OrderBy(release => release.Release.Disambiguation?.Length ?? 0)
                .Select(release => new
                {
                    AlbumName = release.Album,
                    release.Release,
                    AlbumMatch = !string.IsNullOrWhiteSpace(trackAlbum) ? 
                        relaxedFiltering ? FuzzyHelper.PartialTokenSortRatioToLower($"{release.Album} {release.Release.Disambiguation}", trackAlbum) : 
                            FuzzyHelper.FuzzRatioToLower($"{release.Album} {release.Release.Disambiguation}", trackAlbum) : 100,
                    
                    ArtistMatch = !string.IsNullOrWhiteSpace(targetArtist) ? data.ArtistCredit.Sum(artist => FuzzyHelper.FuzzRatioToLower(targetArtist, artist.Name)) : 100,
                    
                    CountryMatch = !string.IsNullOrWhiteSpace(artistCountry) ? relaxedFiltering ? 
                        FuzzyHelper.PartialTokenSortRatioToLower(release.Release.Country, artistCountry) : FuzzyHelper.FuzzRatioToLower(release.Release.Country, artistCountry) : 0,
                    
                    BarcodeMatch = !string.IsNullOrWhiteSpace(release.Barcode) ? relaxedFiltering ? 
                        FuzzyHelper.PartialTokenSortRatioToLower(release.Barcode, trackBarcode) : 
                        FuzzyHelper.FuzzRatioToLower(release.Barcode, trackBarcode) : 0
                })
                .Where(match => relaxedFiltering || FuzzyHelper.ExactNumberMatch(trackAlbum, match.AlbumName))
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

                if (tempTracks?.Count == 0)
                {
                    continue;
                }
                
                media.Tracks = GetBestMatchingTracks(tempTracks, mediaHandler.Title, string.Empty, relaxedFiltering, matchPercentage);

                if (media?.Tracks?.Count == 0 && 
                    !string.IsNullOrWhiteSpace(targetArtist) &&
                    mediaHandler.Title.ToLower().Contains(targetArtist.ToLower()))
                {
                    //try without the artist name in the title for a match
                    string withoutArtistName = mediaHandler.Title.ToLower().Replace(targetArtist.ToLower(), string.Empty);
                    if (withoutArtistName.Length >= MinimumArtistName)
                    {
                        media.Tracks = GetBestMatchingTracks(tempTracks, withoutArtistName, string.Empty, relaxedFiltering, matchPercentage);
                    }
                }

                if (media?.Tracks?.Count == 0 && data?.ArtistCredit?.Count > 1)
                {
                    //try by adding artist credit join phrase
                    string artistCredits = GetArtistFeatCreditString(data.ArtistCredit);
                    media.Tracks = GetBestMatchingTracks(tempTracks, mediaHandler.Title, artistCredits, relaxedFiltering, matchPercentage);
                }

                if (media?.Tracks?.Count == 0 &&
                    data?.ArtistCredit?.Count > 1 &&
                    !string.IsNullOrWhiteSpace(data.ArtistCredit.First().JoinPhrase))
                {
                    //try by removing the join phrase
                    string titleWithoutCredit = CleanupArtistCredit(mediaHandler.Title, data?.ArtistCredit?.First()?.JoinPhrase);
                    
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
            .Where(match => (relaxedFiltering && string.IsNullOrWhiteSpace(trackTitle)) || match.TitleMatch >= matchPercentage)
            .Where(match => relaxedFiltering || FuzzyHelper.ExactNumberMatch(trackTitle, match.Track.Title))
            .Select(releaseTrack => releaseTrack.Track)
            .ToList();;
    }

    public MusicBrainzRecordingQueryReleaseEntityModel? GetBestMatchingRecordingRelease(
        MusicBrainzRecordingQueryEntityModel? data, 
        MediaHandler mediaHandler,
        int matchPercentage,
        ref string trackArtist)
    {
        if (data == null)
        {
            return null;
        }

        foreach (string artistName in mediaHandler.AllArtistNames)
        {
            string trackAlbumWithoutArtist = mediaHandler.Album.Contains(artistName, StringComparison.OrdinalIgnoreCase) ? 
                mediaHandler.Album.ToLower().Replace(artistName.ToLower(), string.Empty) 
                : mediaHandler.Album;
            
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
                        AlbumMatch = !string.IsNullOrWhiteSpace(mediaHandler.Album) ? Math.Max(FuzzyHelper.FuzzRatioToLower(release.Album, mediaHandler.Album), 
                            FuzzyHelper.FuzzRatioToLower(release.Album, trackAlbumWithoutArtist)): 100
                    })
                    .Where(match => FuzzyHelper.ExactNumberMatch(mediaHandler.Album, match.AlbumName))
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
                if (matchedRelease.ArtistCredit == null)
                {
                    matchedRelease.ArtistCredit = data.ArtistCredit;
                }
                
                trackArtist = artistName;
                return matchedRelease;
            }
        }

        return null;
    }

    public async Task<SearchBestMatchingReleaseResult> SearchBestMatchingReleaseAsync(
        MediaHandler mediaHandler,
        MusicBrainzArtistCreditModel? bestMatchedArtist,
        bool relaxedFiltering,
        int matchPercentage)
    {
        var result = new SearchBestMatchingReleaseResult();
        result.BestMatchedArtist = bestMatchedArtist;
        
        string trackArtist = IsVariousArtists(mediaHandler.Artist) && !string.IsNullOrWhiteSpace(bestMatchedArtist?.Name) 
                                ? bestMatchedArtist.Name : mediaHandler.Artist;
        //trackArtist = ArtistHelper.GetUncoupledArtistName(trackArtist);

        if (string.IsNullOrWhiteSpace(trackArtist))
        {
            Logger.WriteLine("Unable to search for a track without the artist name on MusicBrainz", true);
            return result;
        }
        
        var searchResult = await _musicBrainzApiService.SearchReleaseAsync(trackArtist, mediaHandler.Album, mediaHandler.Title);
        
        if (searchResult?.Recordings?.Count == 0)
        {
            //try without the artist name in the title for a match
            string titleWithoutArtistName = mediaHandler.Title.ToLower().Replace(trackArtist.ToLower(), string.Empty);
            string albumWithoutArtistName = mediaHandler.Album.ToLower().Replace(trackArtist.ToLower(), string.Empty);

            if (!string.IsNullOrWhiteSpace(bestMatchedArtist?.JoinPhrase))
            {
                titleWithoutArtistName = CleanupArtistCredit(titleWithoutArtistName, bestMatchedArtist.JoinPhrase);
            }
            
            if (titleWithoutArtistName.Length >= MinimumArtistName && 
                albumWithoutArtistName.Length >= MinimumArtistName)
            {
                searchResult = await _musicBrainzApiService.SearchReleaseAsync(mediaHandler.Artist, albumWithoutArtistName, titleWithoutArtistName);
            }
        }
        
        foreach (var recording in searchResult?.Recordings ?? [])
        {
            var tempRelease = GetBestMatchingRecordingRelease(recording, mediaHandler, matchPercentage, ref trackArtist);
           
            if (tempRelease == null)
            {
                continue;
            }

            result.RecordingQuery = recording;
            var matchedArtist = GetBestMatchingArtist(tempRelease.ArtistCredit, mediaHandler);

            if (matchedArtist == null)
            {
                matchedArtist = GetBestMatchingArtist(recording.ArtistCredit, mediaHandler);
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
                
                var release = GetBestMatchingRelease(musicBrainzArtistModel, mediaHandler, result.ArtistCountry, trackArtist, relaxedFiltering, matchPercentage);

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

    public async Task<GetDataByAcoustIdResult> GetDataByAcoustIdAsync(
        MediaHandler mediaHandler, 
        string acoustIdApiKey,
        int matchPercentage,
        TimeSpan acoustIdMaxTimeSpan)
    {
        GetDataByAcoustIdResult result = new GetDataByAcoustIdResult();
        string? recordingId = string.Empty;
        string? acoustId = string.Empty;
        AcoustIdRecording? matchedRecording = null;

        if (TimeSpan.FromSeconds(mediaHandler.Duration) >= acoustIdMaxTimeSpan)
        {
            Logger.WriteLine($"Track duration is '{mediaHandler.Duration}', AcoustIdMaxTimeSpan is set to '{acoustIdMaxTimeSpan}'", true);
            result.Success = false;
            return result;
        }

        await mediaHandler.GenerateSaveFingerprintAsync();

        if (!string.IsNullOrWhiteSpace(mediaHandler.AcoustId) && 
            Guid.TryParse(mediaHandler.AcoustId, out Guid _))
        {
            Logger.WriteLine($"Looking up AcoustID provided by AcoustId Tag", true);
            
            //try again but with the AcoustID from the media file
            AcoustIdResponse? acoustIdLookup = await _acoustIdService.LookupByAcoustIdAsync(acoustIdApiKey, mediaHandler.AcoustId);
            matchedRecording = await GetBestMatchingAcoustIdAsync(acoustIdLookup, mediaHandler, matchPercentage);
            acoustId = matchedRecording?.AcoustId;
            recordingId = matchedRecording?.Id;
            
            if (!string.IsNullOrWhiteSpace(recordingId) && !string.IsNullOrWhiteSpace(acoustId))
            {
                Logger.WriteLine($"Found AcoustId info from the AcoustId service by the AcoustID provided by the media Tag", true);
            }
        }

        if (string.IsNullOrWhiteSpace(recordingId) && 
            !string.IsNullOrWhiteSpace(mediaHandler.AcoustIdFingerPrint) && 
            mediaHandler.AcoustIdFingerPrintDuration > 0)
        {
            AcoustIdResponse? acoustIdLookup = await _acoustIdService
                .LookupAcoustIdAsync(acoustIdApiKey, mediaHandler.AcoustIdFingerPrint, mediaHandler.Duration);

            matchedRecording = await GetBestMatchingAcoustIdAsync(acoustIdLookup, mediaHandler, matchPercentage);
            acoustId = matchedRecording?.AcoustId;
            recordingId = matchedRecording?.Id;

            if (!string.IsNullOrWhiteSpace(mediaHandler.AcoustId) && 
                !Guid.TryParse(mediaHandler.AcoustId, out Guid _) && 
                (string.IsNullOrWhiteSpace(recordingId) || string.IsNullOrWhiteSpace(acoustId)))
            {
                Logger.WriteLine($"Looking up AcoustID provided by AcoustId Tag", true);
            
                //try again but with the AcoustID from the media file
                acoustIdLookup = await _acoustIdService.LookupByAcoustIdAsync(acoustIdApiKey, mediaHandler.AcoustId);
                matchedRecording = await GetBestMatchingAcoustIdAsync(acoustIdLookup, mediaHandler, matchPercentage);
                acoustId = matchedRecording?.AcoustId;
                recordingId = matchedRecording?.Id;
            
                if (!string.IsNullOrWhiteSpace(recordingId) && !string.IsNullOrWhiteSpace(acoustId))
                {
                    Logger.WriteLine($"Found AcoustId info from the AcoustId service by the AcoustID provided by the media Tag", true);
                }
            }
        }
        
        
        if (string.IsNullOrWhiteSpace(recordingId) || 
            string.IsNullOrWhiteSpace(acoustId))
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

    public string GetArtistFeatCreditString(List<MusicBrainzArtistCreditModel> artists)
    {
        string artistName = string.Empty;

        if (artists.Count > 1)
        {
            int index = 0;
            
            foreach (var artist in artists.Skip(1))
            {
                string joinPhrase = artists.Skip(index).FirstOrDefault()?.JoinPhrase ?? string.Empty;
                
                artistName += $"{joinPhrase}{artist.Name}";
                index++;
            }
        }

        return artistName.Trim();
    }

    public string GetArtistCreditString(List<MusicBrainzArtistCreditModel>? artists)
    {
        string? artistName = string.Empty;

        if (artists?.Count > 1)
        {
            int index = 0;
            foreach (var artist in artists)
            {
                string joinPhrase = artist.JoinPhrase ?? string.Empty;
                artistName += $"{artist.Name}{joinPhrase}";
                index++;
            }
        }

        return artistName?.Trim() ?? string.Empty;
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