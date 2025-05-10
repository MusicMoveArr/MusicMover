using System.Diagnostics;
using System.Runtime.Caching;
using System.Text.RegularExpressions;
using ATL;
using FuzzySharp;
using MusicMover.Helpers;
using MusicMover.Models.Tidal;

namespace MusicMover.Services;

public class TidalService
{
    private readonly string VariousArtists = "Various Artists";
    private readonly string VariousArtistsVA = "VA";
    
    private readonly MediaTagWriteService _mediaTagWriteService;
    private const int MatchPercentage = 80;
    private const int SlidingCacheExpiration = 120;
    private Stopwatch _apiStopwatch = Stopwatch.StartNew();
    private readonly TidalAPICacheLayerService _tidalAPIService;
    
    public TidalService(string tidalClientId, string tidalClientSecret, string countryCode)
    {
        _mediaTagWriteService = new MediaTagWriteService();
        _tidalAPIService = new TidalAPICacheLayerService(tidalClientId, tidalClientSecret, countryCode);
    }
    
    public bool WriteTags(MediaFileInfo mediaFileInfo, FileInfo fromFile, string uncoupledArtistName, string uncoupledAlbumArtist,
        bool overWriteArtist, bool overWriteAlbum, bool overWriteTrack, bool overwriteAlbumArtist)
    {
        if (string.IsNullOrWhiteSpace(_tidalAPIService.AuthenticationResponse?.AccessToken) ||
            (_tidalAPIService.AuthenticationResponse?.ExpiresIn > 0 &&
             DateTime.Now > _tidalAPIService.AuthenticationResponse?.ExpiresAt))
        {
            _tidalAPIService.Authenticate();
        }

        List<string> artistSearch = new List<string>();
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
            Console.WriteLine($"Need to match artist: '{artist}', album: '{mediaFileInfo.Album}', track: '{mediaFileInfo.Title}'");
            Console.WriteLine($"Searching for tidal artist '{artist}'");
            if (TryArtist(artist, mediaFileInfo.Title, mediaFileInfo.Album, fromFile, overWriteArtist, overWriteAlbum, overWriteTrack, overwriteAlbumArtist))
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
                Console.WriteLine($"Need to match artist: '{artist}', album: '{mediaFileInfo.Album}', track: '{mediaFileInfo.Title}'");
                Console.WriteLine($"Searching for tidal artist '{artist}'");
                if (TryArtist(artist, mediaFileInfo.Title, mediaFileInfo.Album, fromFile, overWriteArtist, overWriteAlbum, overWriteTrack, overwriteAlbumArtist))
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    private bool TryArtist(string? artistName, 
        string targetTrackTitle,
        string targetAlbumTitle,
        FileInfo fromFile,
        bool overWriteArtist, 
        bool overWriteAlbum, 
        bool overWriteTrack, 
        bool overwriteAlbumArtist)
    {
        if (string.IsNullOrWhiteSpace(artistName) ||
            IsVariousArtists(artistName))
        {
            return false;
        }
        
        TidalSearchResponse? searchResult;
        searchResult = _tidalAPIService.SearchResultsArtists(artistName);

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
            .Where(match => match.MatchedFor >= MatchPercentage)
            .OrderByDescending(result => result.MatchedFor)
            .ThenByDescending(result => result.Artist.Attributes.Popularity)
            .Select(result => result.Artist)
            .ToList();
        
        foreach (var artist in artists)
        {
            try
            {
                TidalSearchDataEntity? foundAlbum = null;
                TidalSearchDataEntity? foundTrack = null;
                TidalSearchResponse? albumTracks = null;
                List<string>? artistNames = null;
                if (ProcessArtist(artist, targetTrackTitle, targetAlbumTitle, ref foundAlbum, ref foundTrack, ref artistNames, ref albumTracks))
                {
                    return WriteTagsToFile(fromFile, overWriteArtist, overWriteAlbum, overWriteTrack, overwriteAlbumArtist,
                        artist, foundAlbum, foundTrack, artistNames, albumTracks);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"{e.Message}, {e.StackTrace}");
            }
        }

        return false;
    }

    private bool ProcessArtist(TidalSearchDataEntity artist, 
        string targetTrackTitle, 
        string targetAlbumTitle, 
        ref TidalSearchDataEntity? foundAlbum,
        ref TidalSearchDataEntity? foundTrack,
        ref List<string>? artistNames,
        ref TidalSearchResponse? albumTracks)
    {
        Console.WriteLine($"Tidal search query: {artist.Attributes.Name} - {targetTrackTitle}");

        TidalSearchResponse? searchResult = _tidalAPIService.SearchResultsTracks($"{artist.Attributes.Name} - {targetTrackTitle}");
        searchResult = GetAllTracksFromSearch(searchResult);

        List<TidalMatchFound> matchesFound = new List<TidalMatchFound>();
        if (searchResult?.Included != null)
        {
            List<TidalSearchDataEntity> bestTrackMatches = FindBestMatchingTracks(searchResult?.Included, targetTrackTitle, MatchPercentage);
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

                artistNames = GetTrackArtists(int.Parse(result.Id));

                bool containsArtist = artistNames.Any(artistName =>
                                          Fuzz.TokenSortRatio(artist.Attributes.Name, artistName) > MatchPercentage) ||
                                          Fuzz.TokenSortRatio(artist.Attributes.Name, string.Join(' ', artistNames)) > MatchPercentage; //maybe collab?

                if (!containsArtist)
                {
                    continue;
                }

                var album = _tidalAPIService.GetAlbumSelfInfo(result.RelationShips.Albums.Links.Self);
                var albumIds = album.Data
                    .Where(a => a.Type == "albums")
                    .Select(a => int.Parse(a.Id))
                    .Distinct()
                    .ToList();

                foreach (var albumId in albumIds)
                {
                    albumTracks = GetAllTracksByAlbumId(albumId);

                    if (albumTracks?.Included == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(targetAlbumTitle) &&
                        (Fuzz.Ratio(targetAlbumTitle, albumTracks?.Data.Attributes.Title) < MatchPercentage ||
                         !FuzzyHelper.ExactNumberMatch(targetAlbumTitle, albumTracks?.Data.Attributes.Title)))
                    {
                        continue;
                    }

                    var trackMatches = FindBestMatchingTracks(albumTracks?.Included, targetTrackTitle, MatchPercentage);

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
            var tempArtistInfo = _tidalAPIService.GetArtistInfoById(int.Parse(artist.Id));

            if (tempArtistInfo?.Included != null &&
                tempArtistInfo?.Data != null)
            {
                tempArtistInfo = GetAllAlbumsForArtist(tempArtistInfo);
                var matchedAlbums = tempArtistInfo.Included
                    .Where(album => album.Type == "albums")
                    ?.Select(album => new
                    {
                        TitleMatchedFor = Fuzz.Ratio(targetAlbumTitle.ToLower(), album.Attributes.Title.ToLower()),
                        Album = album
                    })
                    .Where(match => FuzzyHelper.ExactNumberMatch(targetAlbumTitle, match.Album.Attributes.Title))
                    .Where(match => match.TitleMatchedFor >= MatchPercentage)
                    .OrderByDescending(result => result.TitleMatchedFor)
                    .Select(result => result.Album)
                    .DistinctBy(album => album.Id)
                    .ToList();

                foreach (var album in matchedAlbums)
                {
                    albumTracks = GetAllTracksByAlbumId(int.Parse(album.Id));
                    foundTrack = FindBestMatchingTracks(albumTracks.Included, targetTrackTitle, MatchPercentage)
                        .FirstOrDefault();

                    if (foundTrack != null)
                    {
                        foundAlbum = album;
                        artistNames = GetTrackArtists(int.Parse(foundTrack.Id));

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

        
        Console.WriteLine("Matches:");
        foreach (var match in bestMatches)
        {
            Console.WriteLine($"Title '{match.Match.FoundTrack.Attributes.Title}' matched for {match.TitleMatchedFor}%, Album '{match.Match.Album.Attributes.Title}' matched for {match.AlbumMatchedFor}%");
        }

        var bestMatch = bestMatches.FirstOrDefault();

        if (bestMatch != null)
        {
            foundTrack = bestMatch.Match.FoundTrack;
            albumTracks = bestMatch.Match.AlbumTracks;
            foundAlbum = bestMatch.Match.Album;
            artistNames = bestMatch.Match.ArtistNames;
            return true;
        }
        return false;
    }

    private List<string> GetTrackArtists(int trackId, string primaryArtistName = "", bool onlyAssociated = false)
    {
        if (trackId == 0)
        {
            return new List<string>();
        }
        
        var trackArtists = _tidalAPIService.GetTrackArtistsByTrackId([trackId]);

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

    private TidalSearchResponse GetAllAlbumsForArtist(TidalSearchResponse artist)
    {
        //fetch all the albums available of the artist
        //by going through the next page cursor
        //populating the artist object
        if (!string.IsNullOrWhiteSpace(artist?.Data?.RelationShips?.Albums?.Links?.Next))
        {
            string? nextPage = artist.Data.RelationShips.Albums.Links.Next;
            while (!string.IsNullOrWhiteSpace(nextPage))
            {
                Console.WriteLine($"Fetching next albums... {artist.Data.RelationShips.Albums.Data.Count}");
                var nextArtistInfo = _tidalAPIService.GetArtistNextInfoById(int.Parse(artist.Data.Id), nextPage);

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

    private bool WriteTagsToFile(FileInfo fromFile,
        bool overWriteArtist, bool overWriteAlbum, bool overWriteTrack, bool overwriteAlbumArtist, 
        TidalSearchDataEntity tidalArtist,
        TidalSearchDataEntity tidalAlbum,
        TidalSearchDataEntity tidalTrack,
        List<string> artistNames,
        TidalSearchResponse albumTracks)
    {
        Track track = new Track(fromFile.FullName);
        bool trackInfoUpdated = false;
        string artists = string.Join(';', artistNames);
        
        Console.WriteLine($"Filpath: {fromFile.FullName}");
        Console.WriteLine($"Tidal Artist: {tidalArtist.Attributes.Name}");
        Console.WriteLine($"Tidal Album: {tidalAlbum.Attributes.Title}");
        Console.WriteLine($"Tidal TrackName: {tidalTrack.Attributes.FullTrackName}");
        Console.WriteLine($"Media Artist: {track.Artist}");
        Console.WriteLine($"Media AlbumArtist: {track.AlbumArtist}");
        Console.WriteLine($"Media Album: {track.Album}");
        Console.WriteLine($"Media TrackName: {track.Title}");
        
        UpdateTag(track, "Tidal Track Id", tidalTrack.Id, ref trackInfoUpdated);
        UpdateTag(track, "Tidal Track Explicit", tidalTrack.Attributes.Explicit ? "Y": "N", ref trackInfoUpdated);
        
        string trackHref = tidalTrack.Attributes.ExternalLinks.FirstOrDefault()?.Href ?? string.Empty;
        UpdateTag(track, "Tidal Track Href", trackHref, ref trackInfoUpdated);
        
        string albumHref = tidalAlbum.Attributes.ExternalLinks.FirstOrDefault()?.Href ?? string.Empty;
        UpdateTag(track, "Tidal Album Id", tidalAlbum.Id, ref trackInfoUpdated);
        UpdateTag(track, "Tidal Album Href", albumHref, ref trackInfoUpdated);
        UpdateTag(track, "Tidal Album Release Date", tidalAlbum.Attributes.ReleaseDate, ref trackInfoUpdated);
        
        string artistHref = tidalArtist.Attributes.ExternalLinks.FirstOrDefault()?.Href ?? string.Empty;
        UpdateTag(track, "Tidal Artist Id", tidalArtist.Id, ref trackInfoUpdated);
        UpdateTag(track, "Tidal Artist Href", artistHref, ref trackInfoUpdated);
        
        if (string.IsNullOrWhiteSpace(track.Title) || overWriteTrack)
        {
            UpdateTag(track, "Title", tidalTrack.Attributes.FullTrackName, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.Album) || overWriteAlbum)
        {
            UpdateTag(track, "Album", tidalAlbum.Attributes.Title, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.AlbumArtist) || track.AlbumArtist.ToLower().Contains("various") || overwriteAlbumArtist)
        {
            UpdateTag(track, "AlbumArtist", tidalArtist.Attributes.Name, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.Artist) || track.Artist.ToLower().Contains("various") || overWriteArtist)
        {
            UpdateTag(track, "Artist",  tidalArtist.Attributes.Name, ref trackInfoUpdated);
        }
        UpdateTag(track, "ARTISTS", artists, ref trackInfoUpdated);

        UpdateTag(track, "ISRC", tidalTrack.Attributes.ISRC, ref trackInfoUpdated);
        UpdateTag(track, "UPC", tidalAlbum.Attributes.BarcodeId, ref trackInfoUpdated);
        UpdateTag(track, "Date", tidalAlbum.Attributes.ReleaseDate, ref trackInfoUpdated);
        UpdateTag(track, "Copyright", tidalTrack.Attributes.Copyright, ref trackInfoUpdated);
        
        var trackNumber = albumTracks.Data
            .RelationShips
            .Items
            .Data
            .FirstOrDefault(x => x.Id == tidalTrack.Id);

        if (trackNumber != null)
        {
            UpdateTag(track, "Disc Number", trackNumber.Meta.VolumeNumber.ToString(), ref trackInfoUpdated);
            UpdateTag(track, "Track Number", trackNumber.Meta.TrackNumber.ToString(), ref trackInfoUpdated);
            UpdateTag(track, "Total Tracks", tidalAlbum.Attributes.NumberOfItems.ToString(), ref trackInfoUpdated);
        }
        
        _mediaTagWriteService.SafeSave(track);
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
    

    private TidalSearchResponse? GetAllTracksByAlbumId(int albumId)
    {
        var tracks = _tidalAPIService.GetTracksByAlbumId(albumId);
        Console.WriteLine($"Getting tracks of album '{tracks?.Data.Attributes.Title}', album id '{albumId}'");
        if (tracks?.Data.Attributes.NumberOfItems >= 20)
        {
            string? nextPage = tracks.Data.RelationShips?.Items?.Links?.Next;
            while (!string.IsNullOrWhiteSpace(nextPage))
            {
                var tempTracks = _tidalAPIService.GetTracksNextByAlbumId(albumId, nextPage);

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
    private TidalSearchResponse? GetAllTracksFromSearch(TidalSearchResponse searchResults)
    {
        if (searchResults?.Included?.Count >= 20)
        {
            string? nextPage = searchResults.Data.RelationShips?.Tracks?.Links?.Next;
            while (!string.IsNullOrWhiteSpace(nextPage))
            {
                var tempTracks = _tidalAPIService.GetTracksNextFromSearch(nextPage);

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
}