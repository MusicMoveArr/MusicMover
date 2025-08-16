using System.Diagnostics;
using System.Runtime.Caching;
using System.Text.RegularExpressions;
using ATL;
using FuzzySharp;
using MusicMover.Helpers;
using MusicMover.Models;
using MusicMover.Models.Tidal;
using Spectre.Console;

namespace MusicMover.Services;

public class TidalService
{
    private readonly string VariousArtists = "Various Artists";
    private readonly string VariousArtistsVA = "VA";
    
    private readonly MediaTagWriteService _mediaTagWriteService;
    private const int SlidingCacheExpiration = 120;
    private Stopwatch _apiStopwatch = Stopwatch.StartNew();
    private readonly TidalAPICacheLayerService _tidalAPIService;
    
    public TidalService(string tidalClientId, string tidalClientSecret, string countryCode)
    {
        _mediaTagWriteService = new MediaTagWriteService();
        _tidalAPIService = new TidalAPICacheLayerService(tidalClientId, tidalClientSecret, countryCode);
    }
    
    public async Task<bool> WriteTagsAsync(
        MediaFileInfo mediaFileInfo, 
        FileInfo fromFile, string uncoupledArtistName, 
        string uncoupledAlbumArtist,
        bool overWriteArtist, 
        bool overWriteAlbum, 
        bool overWriteTrack, 
        bool overwriteAlbumArtist,
        int matchPercentage)
    {
        if (string.IsNullOrWhiteSpace(_tidalAPIService.AuthenticationResponse?.AccessToken) ||
            (_tidalAPIService.AuthenticationResponse?.ExpiresIn > 0 &&
             DateTime.Now > _tidalAPIService.AuthenticationResponse?.ExpiresAt))
        {
            await _tidalAPIService.AuthenticateAsync();
        }

        List<string?> artistSearch = new List<string?>();
        artistSearch.Add(mediaFileInfo.Artist);
        artistSearch.Add(mediaFileInfo.AlbumArtist);
        artistSearch.Add(uncoupledArtistName);
        artistSearch.Add(uncoupledAlbumArtist);
        
        artistSearch = artistSearch
            .Where(artist => !string.IsNullOrWhiteSpace(artist))
            .DistinctBy(artist => artist)
            .ToList();

        if (!artistSearch.Any())
        {
            return false;
        }

        foreach (var artist in artistSearch)
        {
            Logger.WriteLine($"Need to match artist: '{artist}', album: '{mediaFileInfo.Album}', track: '{mediaFileInfo.Title}'", true);
            Logger.WriteLine($"Searching for tidal artist '{artist}'", true);
            if (await TryArtistAsync(artist, mediaFileInfo.Title!, mediaFileInfo.Album, fromFile, overWriteArtist, overWriteAlbum, overWriteTrack, overwriteAlbumArtist, matchPercentage))
            {
                return true;
            }
        }

        //try again without numbers at the start of the track name
        //some like to add the TrackNumber to the title tag...
        //we need to check again in case it wasn't a TrackNumber but a year number etc
        if (Regex.IsMatch(mediaFileInfo.Title, "^[0-9]*"))
        {
            mediaFileInfo.Title = Regex.Replace(mediaFileInfo.Title, "^[0-9]*", string.Empty).TrimStart();
            
            foreach (var artist in artistSearch)
            {
                Logger.WriteLine($"Need to match artist: '{artist}', album: '{mediaFileInfo.Album}', track: '{mediaFileInfo.Title}'", true);
                Logger.WriteLine($"Searching for tidal artist '{artist}'", true);
                if (await TryArtistAsync(artist, mediaFileInfo.Title, mediaFileInfo.Album, fromFile, overWriteArtist, overWriteAlbum, overWriteTrack, overwriteAlbumArtist, matchPercentage))
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    private async Task<bool> TryArtistAsync(string? artistName, 
        string targetTrackTitle,
        string? targetAlbumTitle,
        FileInfo fromFile,
        bool overWriteArtist, 
        bool overWriteAlbum, 
        bool overWriteTrack, 
        bool overwriteAlbumArtist,
        int matchPercentage)
    {
        if (string.IsNullOrWhiteSpace(artistName) ||
            IsVariousArtists(artistName))
        {
            return false;
        }
        
        TidalSearchResponse? searchResult;
        searchResult = await _tidalAPIService.SearchResultsArtistsAsync(artistName);

        if (searchResult?.Included == null)
        {
            return false;
        }

        if (searchResult?.Included == null)
        {
            return false;
        }

        var artists = searchResult.Included
            .Where(artist => artist.Type == "artists")
            .Where(artist => !string.IsNullOrWhiteSpace(artist?.Attributes?.Name))
            .Select(artist => new
            {
                MatchedFor = Fuzz.Ratio(artistName, artist.Attributes.Name),
                Artist = artist
            })
            .Where(match => FuzzyHelper.ExactNumberMatch(artistName, match.Artist.Attributes.Name))
            .Where(match => match.MatchedFor >= matchPercentage)
            .OrderByDescending(result => result.MatchedFor)
            .ThenByDescending(result => result.Artist.Attributes.Popularity)
            .Select(result => result.Artist)
            .ToList();
        
        foreach (var artist in artists)
        {
            try
            {
                TidalProcessArtistResult processResult = await ProcessArtistAsync(artist, targetTrackTitle, targetAlbumTitle, matchPercentage);
                
                if (processResult.Success)
                {
                    return await WriteTagsToFileAsync(fromFile, overWriteArtist, overWriteAlbum, overWriteTrack, overwriteAlbumArtist,
                        artist, processResult.FoundAlbum, processResult.FoundTrack, processResult.ArtistNames, processResult.AlbumTracks);
                }
            }
            catch (Exception e)
            {
                Logger.WriteLine($"{e.Message}, {e.StackTrace}");
            }
        }

        return false;
    }

    private async Task<TidalProcessArtistResult> ProcessArtistAsync(TidalSearchDataEntity artist, 
        string targetTrackTitle, 
        string? targetAlbumTitle,
        int matchPercentage)
    {
        TidalSearchDataEntity? foundAlbum;
        TidalSearchDataEntity? foundTrack;
        List<string>? artistNames;
        TidalSearchResponse? albumTracks;
        
        Logger.WriteLine($"Tidal search query: {artist.Attributes.Name} - {targetTrackTitle}", true);

        TidalSearchResponse? searchResult = await _tidalAPIService.SearchResultsTracksAsync($"{artist.Attributes.Name} - {targetTrackTitle}");
        searchResult = await GetAllTracksFromSearchAsync(searchResult);

        List<TidalMatchFound> matchesFound = new List<TidalMatchFound>();
        if (searchResult?.Included != null)
        {
            List<TidalSearchDataEntity> bestTrackMatches = FindBestMatchingTracks(searchResult?.Included, targetTrackTitle, matchPercentage);
            bestTrackMatches = bestTrackMatches
                .Where(track => !string.IsNullOrWhiteSpace(track.RelationShips.Albums.Links.Self))
                .DistinctBy(track => new
                {
                    track.Id,
                    track.RelationShips.Albums.Links.Self
                })
                .ToList();
            
            TidalSearchDataEntity? tempFoundTrack = null;
            TidalSearchResponse? tempAlbumTracks = null;
            TidalSearchDataEntity? tempAlbum = null;
            List<string>? tempArtistNames = null;

            foreach (var result in bestTrackMatches)
            {
                if (matchesFound.Count >= 1 &&
                    string.IsNullOrWhiteSpace(targetAlbumTitle))
                {
                    break;
                }

                artistNames = await GetTrackArtistsAsync(int.Parse(result.Id));

                bool containsArtist = artistNames.Any(artistName =>
                                          Fuzz.TokenSortRatio(artist.Attributes.Name, artistName) > matchPercentage) ||
                                      Fuzz.TokenSortRatio(artist.Attributes.Name, string.Join(' ', artistNames)) > matchPercentage; //maybe collab?

                if (!containsArtist)
                {
                    continue;
                }

                var album = await _tidalAPIService.GetAlbumSelfInfoAsync(result.RelationShips.Albums.Links.Self);
                var albumIds = album.Data
                    .Where(a => a.Type == "albums")
                    .Select(a => int.Parse(a.Id))
                    .Distinct()
                    .ToList();

                foreach (var albumId in albumIds)
                {
                    albumTracks = await GetAllTracksByAlbumIdAsync(albumId);

                    if (albumTracks?.Included == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(targetAlbumTitle) &&
                        (Fuzz.Ratio(targetAlbumTitle, albumTracks.Data.Attributes.Title) < matchPercentage ||
                         !FuzzyHelper.ExactNumberMatch(targetAlbumTitle, albumTracks?.Data.Attributes.Title)))
                    {
                        continue;
                    }

                    var trackMatches = FindBestMatchingTracks(albumTracks.Included, targetTrackTitle, matchPercentage);

                    foreach (var trackMatch in trackMatches)
                    {
                        tempFoundTrack = trackMatch;
                        tempAlbumTracks = albumTracks;
                        tempAlbum = albumTracks.Data;
                        tempArtistNames = artistNames;
                        matchesFound.Add(new TidalMatchFound(tempFoundTrack, tempAlbumTracks, tempAlbum, tempArtistNames));
                    }

                    if (string.IsNullOrWhiteSpace(targetAlbumTitle))
                    {
                        //first match wins, unable to compare album to anything
                        break;
                    }
                }
            }
        }

        //try by fetching all albums and going through them...
        if (matchesFound.Count == 0 && !string.IsNullOrWhiteSpace(targetAlbumTitle))
        {
            var tempArtistInfo = await _tidalAPIService.GetArtistInfoByIdAsync(int.Parse(artist.Id));

            if (tempArtistInfo?.Included != null &&
                tempArtistInfo?.Data != null)
            {
                tempArtistInfo = await GetAllAlbumsForArtistAsync(tempArtistInfo);
                var matchedAlbums = tempArtistInfo.Included
                    .Where(album => album.Type == "albums")
                    ?.Select(album => new
                    {
                        TitleMatchedFor = Fuzz.Ratio(targetAlbumTitle.ToLower(), album.Attributes.Title.ToLower()),
                        Album = album
                    })
                    .Where(match => FuzzyHelper.ExactNumberMatch(targetAlbumTitle, match.Album.Attributes.Title))
                    .Where(match => match.TitleMatchedFor >= matchPercentage)
                    .OrderByDescending(result => result.TitleMatchedFor)
                    .Select(result => result.Album)
                    .DistinctBy(album => album.Id)
                    .ToList();

                foreach (var album in matchedAlbums)
                {
                    albumTracks = await GetAllTracksByAlbumIdAsync(int.Parse(album.Id));
                    foundTrack = FindBestMatchingTracks(albumTracks.Included, targetTrackTitle, matchPercentage)
                        .FirstOrDefault();

                    if (foundTrack != null)
                    {
                        foundTrack = album;
                        artistNames = await GetTrackArtistsAsync(int.Parse(foundTrack.Id));

                        matchesFound.Add(new TidalMatchFound(foundTrack, albumTracks, album, artistNames));
                    }
                }
            }
        }
        
        
        var bestMatches = matchesFound
            .Select(match => new
            {
                TitleMatchedFor = Fuzz.Ratio(targetTrackTitle, match.FoundTrack.Attributes.Title),
                AlbumMatchedFor = Fuzz.Ratio(targetAlbumTitle, match.Album.Attributes.Title),
                Match = match
            })
            .OrderByDescending(result => result.TitleMatchedFor)
            .ThenByDescending(result => result.AlbumMatchedFor)
            .ToList();

        
        Logger.WriteLine("Matches:", true);
        foreach (var match in bestMatches)
        {
            Logger.WriteLine($"Title '{match.Match.FoundTrack.Attributes.Title}' matched for {match.TitleMatchedFor}%, Album '{match.Match.Album.Attributes.Title}' matched for {match.AlbumMatchedFor}%", true);
        }

        var bestMatch = bestMatches.FirstOrDefault();
        TidalProcessArtistResult processRresult = new TidalProcessArtistResult();
        
        if (bestMatch != null)
        {
            processRresult.FoundTrack = bestMatch.Match.FoundTrack;
            processRresult.AlbumTracks = bestMatch.Match.AlbumTracks;
            processRresult.FoundAlbum = bestMatch.Match.Album;
            processRresult.ArtistNames = bestMatch.Match.ArtistNames;
            processRresult.Success = true;
        }
        return processRresult;
    }

    private async Task<List<string>> GetTrackArtistsAsync(int trackId, string primaryArtistName = "", bool onlyAssociated = false)
    {
        if (trackId == 0)
        {
            return new List<string>();
        }
        
        var trackArtists = await _tidalAPIService.GetTrackArtistsByTrackIdAsync([trackId]);

        if (trackArtists?.Included == null)
        {
            return new List<string>();
        }

        var artistNames = trackArtists.Included
            .Where(artistName => !string.IsNullOrWhiteSpace(artistName.Attributes.Name))
            .Select(artistName => artistName.Attributes.Name)
            .ToList()!;

        if (onlyAssociated)
        {
            artistNames = artistNames
                .Where(artistName => !string.Equals(artistName, primaryArtistName))
                .ToList();
        }
        return artistNames;
    }

    private async Task<TidalSearchResponse> GetAllAlbumsForArtistAsync(TidalSearchResponse artist)
    {
        //fetch all the albums available of the artist
        //by going through the next page cursor
        //populating the artist object
        if (!string.IsNullOrWhiteSpace(artist?.Data?.RelationShips?.Albums?.Links?.Next))
        {
            string? nextPage = artist.Data.RelationShips.Albums.Links.Next;
            while (!string.IsNullOrWhiteSpace(nextPage))
            {
                Logger.WriteLine($"Fetching next albums... {artist.Data.RelationShips.Albums.Data.Count}", true);
                var nextArtistInfo = await _tidalAPIService.GetArtistNextInfoByIdAsync(int.Parse(artist.Data.Id), nextPage);

                if (nextArtistInfo?.Data?.Count > 0)
                {
                    artist.Data.RelationShips.Albums.Data.AddRange(nextArtistInfo.Data);
                }

                if (nextArtistInfo?.Included?.Count > 0)
                {
                    artist.Included.AddRange(nextArtistInfo.Included);
                }

                nextPage = nextArtistInfo?.Links?.Next;
            }
        }

        return artist;
    }

    private List<TidalSearchDataEntity> FindBestMatchingTracks(
        List<TidalSearchDataEntity> searchResults, 
        string targetTrackTitle,
        int matchRatioPercentage)
    {
        //strict name matching
        return searchResults
            ?.Where(t => t.Type == "tracks")
            ?.Select(t => new
            {
                TitleMatchedFor = Fuzz.Ratio(targetTrackTitle?.ToLower(), t.Attributes.FullTrackName.ToLower()),
                Track = t
            })
            .Where(match => FuzzyHelper.ExactNumberMatch(targetTrackTitle, match.Track.Attributes.FullTrackName))
            .Where(match => match.TitleMatchedFor >= matchRatioPercentage)
            .OrderByDescending(result => result.TitleMatchedFor)
            .Select(result => result.Track)
            .ToList() ?? [];
    }

    private async Task<bool> WriteTagsToFileAsync(FileInfo fromFile,
        bool overWriteArtist, 
        bool overWriteAlbum, 
        bool overWriteTrack, 
        bool overwriteAlbumArtist, 
        TidalSearchDataEntity tidalArtist,
        TidalSearchDataEntity tidalAlbum,
        TidalSearchDataEntity tidalTrack,
        List<string> artistNames,
        TidalSearchResponse albumTracks)
    {
        Track track = new Track(fromFile.FullName);
        bool trackInfoUpdated = false;
        string artists = string.Join(';', artistNames);
        
        Logger.WriteLine($"Filpath: {fromFile.FullName}", true);
        Logger.WriteLine($"Tidal Artist: {tidalArtist.Attributes.Name}", true);
        Logger.WriteLine($"Tidal Album: {tidalAlbum.Attributes.Title}", true);
        Logger.WriteLine($"Tidal TrackName: {tidalTrack.Attributes.FullTrackName}", true);
        Logger.WriteLine($"Media Artist: {track.Artist}", true);
        Logger.WriteLine($"Media AlbumArtist: {track.AlbumArtist}", true);
        Logger.WriteLine($"Media Album: {track.Album}", true);
        Logger.WriteLine($"Media TrackName: {track.Title}", true);
        
        _mediaTagWriteService.UpdateTag(track, "Tidal Track Id", tidalTrack.Id, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(track, "Tidal Track Explicit", tidalTrack.Attributes.Explicit ? "Y": "N", ref trackInfoUpdated);
        
        string trackHref = tidalTrack.Attributes.ExternalLinks.FirstOrDefault()?.Href ?? string.Empty;
        _mediaTagWriteService.UpdateTag(track, "Tidal Track Href", trackHref, ref trackInfoUpdated);
        
        string albumHref = tidalAlbum.Attributes.ExternalLinks.FirstOrDefault()?.Href ?? string.Empty;
        _mediaTagWriteService.UpdateTag(track, "Tidal Album Id", tidalAlbum.Id, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(track, "Tidal Album Href", albumHref, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(track, "Tidal Album Release Date", tidalAlbum.Attributes.ReleaseDate, ref trackInfoUpdated);
        
        string artistHref = tidalArtist.Attributes.ExternalLinks.FirstOrDefault()?.Href ?? string.Empty;
        _mediaTagWriteService.UpdateTag(track, "Tidal Artist Id", tidalArtist.Id, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(track, "Tidal Artist Href", artistHref, ref trackInfoUpdated);
        
        if (string.IsNullOrWhiteSpace(track.Title) || overWriteTrack)
        {
            _mediaTagWriteService.UpdateTag(track, "Title", tidalTrack.Attributes.FullTrackName, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.Album) || overWriteAlbum)
        {
            _mediaTagWriteService.UpdateTag(track, "Album", tidalAlbum.Attributes.Title, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.AlbumArtist) || track.AlbumArtist.ToLower().Contains("various") || overwriteAlbumArtist)
        {
            _mediaTagWriteService.UpdateTag(track, "AlbumArtist", tidalArtist.Attributes.Name, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.Artist) || track.Artist.ToLower().Contains("various") || overWriteArtist)
        {
            _mediaTagWriteService.UpdateTag(track, "Artist",  tidalArtist.Attributes.Name, ref trackInfoUpdated);
        }
        _mediaTagWriteService.UpdateTag(track, "ARTISTS", artists, ref trackInfoUpdated);

        _mediaTagWriteService.UpdateTag(track, "ISRC", tidalTrack.Attributes.ISRC, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(track, "UPC", tidalAlbum.Attributes.BarcodeId, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(track, "Date", tidalAlbum.Attributes.ReleaseDate, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(track, "Copyright", tidalTrack.Attributes.Copyright, ref trackInfoUpdated);
        
        var trackNumber = albumTracks.Data
            .RelationShips
            .Items
            .Data
            .FirstOrDefault(x => x.Id == tidalTrack.Id);

        if (trackNumber != null)
        {
            _mediaTagWriteService.UpdateTag(track, "Disc Number", trackNumber.Meta.VolumeNumber.ToString(), ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(track, "Track Number", trackNumber.Meta.TrackNumber.ToString(), ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(track, "Total Tracks", tidalAlbum.Attributes.NumberOfItems.ToString(), ref trackInfoUpdated);
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
    

    private async Task<TidalSearchResponse?> GetAllTracksByAlbumIdAsync(int albumId)
    {
        var tracks = await _tidalAPIService.GetTracksByAlbumIdAsync(albumId);
        Logger.WriteLine($"Getting tracks of album '{tracks?.Data.Attributes.Title}', album id '{albumId}'", true);
        if (tracks?.Data.Attributes.NumberOfItems >= 20)
        {
            string? nextPage = tracks.Data.RelationShips?.Items?.Links?.Next;
            while (!string.IsNullOrWhiteSpace(nextPage))
            {
                var tempTracks = await _tidalAPIService.GetTracksNextByAlbumIdAsync(albumId, nextPage);

                if (tempTracks?.Included?.Count > 0)
                {
                    tracks.Included.AddRange(tempTracks.Included);
                }

                if (tempTracks?.Data?.Count > 0)
                {
                    tracks.Data
                        ?.RelationShips
                        ?.Items
                        ?.Data
                        ?.AddRange(tempTracks.Data);
                }
                nextPage = tempTracks?.Links?.Next;
            }
        }
        return tracks;
    }
    private async Task<TidalSearchResponse?> GetAllTracksFromSearchAsync(TidalSearchResponse searchResults)
    {
        if (searchResults?.Included?.Count >= 20)
        {
            string? nextPage = searchResults.Data.RelationShips?.Tracks?.Links?.Next;
            while (!string.IsNullOrWhiteSpace(nextPage))
            {
                var tempTracks = await _tidalAPIService.GetTracksNextFromSearchAsync(nextPage);

                if (tempTracks?.Included?.Count > 0)
                {
                    searchResults.Included.AddRange(tempTracks.Included);
                }

                if (tempTracks?.Data?.Count > 0)
                {
                    searchResults.Data
                        ?.RelationShips
                        ?.Items
                        ?.Data
                        ?.AddRange(tempTracks.Data);
                }
                nextPage = tempTracks?.Links?.Next;
            }
        }
        
        return searchResults;
    }
}