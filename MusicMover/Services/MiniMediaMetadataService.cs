using System.Text.RegularExpressions;
using ATL;
using MusicMover.Helpers;
using MusicMover.Models.MetadataAPI.Entities;

namespace MusicMover.Services;

public class MiniMediaMetadataService
{
    private readonly string VariousArtists = "Various Artists";
    private readonly string VariousArtistsVA = "VA";
    private readonly MiniMediaMetadataAPIService _miniMediaMetadataApiService;
    private readonly MediaTagWriteService _mediaTagWriteService;

    private readonly List<string> _providerTypes;
    
    public MiniMediaMetadataService(string baseUrl, List<string> providerTypes)
    {
        _providerTypes = providerTypes;
        _mediaTagWriteService = new MediaTagWriteService();
        _miniMediaMetadataApiService = new MiniMediaMetadataAPIService(baseUrl, providerTypes);
    }

    public async Task<List<SearchTrackEntity>> GetMatchesAsync(
        MediaFileInfo mediaFileInfo,
        string uncoupledArtistName,
        string uncoupledAlbumArtist,
        int matchPercentage)
    {
        if (string.IsNullOrWhiteSpace(mediaFileInfo.Artist) || string.IsNullOrWhiteSpace(mediaFileInfo.Title))
        {
            return new List<SearchTrackEntity>();
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
            return new List<SearchTrackEntity>();
        }
        
        //replace disc X from album
        string targetAlbum = mediaFileInfo?.Album ?? string.Empty;
        string discPattern = @"(?:[\[(])?(disc|cd)\s*([0-9]+)[\])]?";
        if (Regex.IsMatch(targetAlbum.ToLower(), discPattern))
        {
            targetAlbum = Regex.Replace(targetAlbum.ToLower(), discPattern, string.Empty).TrimEnd();
        }

        foreach (var artist in artistSearch)
        {
            Logger.WriteLine($"Need to match artist: '{artist}', album: '{targetAlbum}', track: '{mediaFileInfo.Title}'", true);
            Logger.WriteLine($"Searching for artist '{artist}'", true);

            List<SearchTrackEntity> foundTracks = await TryArtistAsync(artist, mediaFileInfo.Title, targetAlbum, matchPercentage);
            if (foundTracks.Count > 0)
            {
                return foundTracks;
            }
        }

        string? artistInAlbumName = artistSearch.FirstOrDefault(artist => targetAlbum.ToLower().Contains(artist.ToLower()));
        if (!string.IsNullOrWhiteSpace(artistInAlbumName))
        {
            string withoutArtistInAlbum = targetAlbum.ToLower().Replace(artistInAlbumName.ToLower(), string.Empty);
            foreach (var artist in artistSearch)
            {
                Logger.WriteLine($"Need to match artist: '{artist}', album: '{withoutArtistInAlbum}', track: '{mediaFileInfo.Title}'", true);
                Logger.WriteLine($"Searching for artist '{artist}'", true);

                List<SearchTrackEntity> foundTracks = await TryArtistAsync(artist, mediaFileInfo.Title, withoutArtistInAlbum, matchPercentage);
                if (foundTracks.Count > 0)
                {
                    return foundTracks;
                }
            }
        }
        return new List<SearchTrackEntity>();
    }
    
    private async Task<List<SearchTrackEntity>> TryArtistAsync(string? artistName, 
        string targetTrackTitle,
        string targetAlbumTitle,
        int matchPercentage)
    {
        List<SearchTrackEntity> foundTracks = new List<SearchTrackEntity>();
        if (string.IsNullOrWhiteSpace(artistName) ||
            IsVariousArtists(artistName))
        {
            return foundTracks;
        }

        var searchResult = await _miniMediaMetadataApiService.SearchArtistsAsync(artistName);

        if (!searchResult.Artists.Any())
        {
            return foundTracks;
        }
        
        foreach (string provider in _providerTypes)
        {
            var artists = searchResult.Artists
                .Where(artist => !string.IsNullOrWhiteSpace(artist.Name))
                .Where(artist => string.Equals(artist.ProviderType, provider) || string.Equals(provider, "any", StringComparison.OrdinalIgnoreCase))
                .Select(artist => new
                {
                    MatchedFor = FuzzyHelper.FuzzRatioToLower(artistName, artist.Name),
                    Artist = artist
                })
                .Where(match => FuzzyHelper.ExactNumberMatch(artistName, match.Artist.Name))
                .Where(match => match.MatchedFor >= matchPercentage)
                .OrderByDescending(result => result.MatchedFor)
                .ThenByDescending(result => result.Artist.Popularity)
                .Select(result => result.Artist)
                .Take(10)
                .ToList();
            
            foreach (var artist in artists)
            {
                try
                {
                    SearchTrackEntity? foundTrack = await ProcessArtistAsync(artist, targetTrackTitle, targetAlbumTitle, matchPercentage);
                    
                    if (foundTrack != null)
                    {
                        foundTracks.Add(foundTrack);
                        break;
                    }
                }
                catch (Exception e)
                {
                    Logger.WriteLine($"{e.Message}, {e.StackTrace}");
                }
            }
        }
        return foundTracks;
    }

    private async Task<SearchTrackEntity?> ProcessArtistAsync(
        SearchArtistEntity artist,
        string targetTrackTitle,
        string targetAlbumTitle,
        int matchPercentage)
    {
        Logger.WriteLine($"MiniMedia Metadata API, search query: '{artist.Name} - {targetTrackTitle}', Provider: {artist.ProviderType}, ArtistId: '{artist.Id}'", true);
        var searchResultTracks = await _miniMediaMetadataApiService.SearchTracksAsync(targetTrackTitle, artist.Id, artist.ProviderType);

        List<SearchTrackEntity> matchesFound = new List<SearchTrackEntity>();
        List<SearchTrackEntity> bestTrackMatches = FindBestMatchingTracks(searchResultTracks.Tracks, targetTrackTitle, matchPercentage);

        foreach (var result in bestTrackMatches)
        {
            if (matchesFound.Count >= 1 &&
                string.IsNullOrWhiteSpace(targetAlbumTitle))
            {
                break;
            }

            var artistNames = result.Artists
                .Select(artist => artist.Name)
                .ToList();

            bool containsArtist = artistNames.Any(artistName =>
                                      FuzzyHelper.FuzzTokenSortRatioToLower(artist.Name, artistName) > matchPercentage) ||
                                      FuzzyHelper.FuzzTokenSortRatioToLower(artist.Name, string.Join(' ', artistNames)) > matchPercentage; //maybe collab?

            if (!containsArtist)
            {
                continue;
            }
            
            if (!string.IsNullOrWhiteSpace(targetAlbumTitle) &&
                (FuzzyHelper.FuzzRatioToLower(targetAlbumTitle, result.Album.Name) < matchPercentage ||
                 !FuzzyHelper.ExactNumberMatch(targetAlbumTitle, result.Album.Name)))
            {
                continue;
            }

            var trackMatches = FindBestMatchingTracks([result], targetTrackTitle, matchPercentage);

            if(trackMatches.Count > 0)
            {
                matchesFound.Add(result);
            }

            if (string.IsNullOrWhiteSpace(targetAlbumTitle))
            {
                //first match wins, unable to compare album to anything
                break;
            }
        }
        
        var bestMatches = matchesFound
            .Select(match => new
            {
                TitleMatchedFor = FuzzyHelper.FuzzRatioToLower(targetTrackTitle, match.Name),
                AlbumMatchedFor = FuzzyHelper.FuzzRatioToLower(targetAlbumTitle, match.Album.Name),
                Match = match
            })
            .OrderByDescending(result => result.TitleMatchedFor)
            .ThenByDescending(result => result.AlbumMatchedFor)
            .ToList();
        
        //Logger.WriteLine("Tracks to match with:");
        //Logger.WriteLine($"Match with, Title '{targetTrackTitle}', Album '{targetAlbumTitle}', Artist: '{artist.Name}'");
        //foreach (var match in searchResultTracks.Tracks)
        //{
        //    string? mainArtist = match.Artists.FirstOrDefault(artist => artist.Id == match.Album.ArtistId)?.Name;
        //    Logger.WriteLine($"Title '{match.Name}', Album '{match.Album.Name}', Artist: '{mainArtist}'");
        //}

        if (bestMatches.Count > 0)
        {
            Logger.WriteLine("Matches:", true);
            foreach (var match in bestMatches)
            {
                Logger.WriteLine($"Title '{match.Match.Name}' matched for {match.TitleMatchedFor}%, Album '{match.Match.Album.Name}' matched for {match.AlbumMatchedFor}%", true);
            }
        }

        var bestMatch = bestMatches.FirstOrDefault();

        if (bestMatch != null)
        {
            return bestMatch.Match;
        }

        return null;
    }
    
    private List<SearchTrackEntity> FindBestMatchingTracks(
        List<SearchTrackEntity> searchResults, 
        string targetTrackTitle,
        int matchRatioPercentage)
    {
        //strict name matching
        return searchResults
            ?.Select(t => new
            {
                TitleMatchedFor = FuzzyHelper.FuzzRatioToLower(targetTrackTitle, t.Name),
                Track = t
            })
            .Where(match => FuzzyHelper.ExactNumberMatch(targetTrackTitle, match.Track.Name))
            .Where(match => match.TitleMatchedFor >= matchRatioPercentage)
            .OrderByDescending(result => result.TitleMatchedFor)
            .Select(result => result.Track)
            .ToList() ?? [];
    }

    private bool IsVariousArtists(string? name)
    {
        if (name?.ToLower() == VariousArtistsVA.ToLower())
        {
            return true;
        }

        if (FuzzyHelper.FuzzRatioToLower(name, VariousArtists) >= 95)
        {
            return true;
        }

        return false;
    }
    
    public async Task<bool> WriteTagsToFileAsync(
        SearchTrackEntity foundTrack, 
        MediaFileInfo mediaFileInfo,
        bool overWriteArtist, 
        bool overWriteAlbum, 
        bool overWriteTrack, 
        bool overwriteAlbumArtist)
    {
        bool trackInfoUpdated = false;
        string artists = string.Join(';', foundTrack.Artists.Select(artist => artist.Name));

        string? mainArtist = foundTrack.Artists.FirstOrDefault(artist => artist.Id == foundTrack.Album.ArtistId)?.Name;

        if (string.IsNullOrWhiteSpace(mainArtist))
        {
            Logger.WriteLine("Main artist is missing, bug in MiniMedia's Metadata API?");
            return false;
        }
        
        Logger.WriteLine($"Filpath: {mediaFileInfo.FileInfo.FullName}", true);
        Logger.WriteLine($"Provider: {foundTrack.ProviderType}", true);
        Logger.WriteLine($"API Artist: {mainArtist}", true);
        Logger.WriteLine($"API Album: {foundTrack.Album.Name}", true);
        Logger.WriteLine($"API TrackName: {foundTrack.Name}", true);
        Logger.WriteLine($"Media Artist: {mediaFileInfo.Artist}", true);
        Logger.WriteLine($"Media AlbumArtist: {mediaFileInfo.AlbumArtist}", true);
        Logger.WriteLine($"Media Album: {mediaFileInfo.Album}", true);
        Logger.WriteLine($"Media TrackName: {mediaFileInfo.Title}", true);

        if (foundTrack.ProviderType == "Tidal")
        {
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Tidal Track Id", foundTrack.Id, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Tidal Track Explicit", foundTrack.Explicit ? "Y": "N", ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Tidal Track Href", foundTrack.Url, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Tidal Album Id", foundTrack.Album.Id, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Tidal Album Href", foundTrack.Album.Url, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Tidal Album Release Date", foundTrack.Album.ReleaseDate, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Tidal Artist Id", foundTrack.Album.ArtistId, ref trackInfoUpdated);
        }
        else if (foundTrack.ProviderType == "MusicBrainz")
        {
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Artist Id", foundTrack.MusicBrainz.ArtistId, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Track Id", foundTrack.MusicBrainz.RecordingId, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Release Track Id", foundTrack.MusicBrainz.ReleaseTrackId, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Release Artist Id", foundTrack.MusicBrainz.ReleaseArtistId, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Release Group Id", foundTrack.MusicBrainz.ReleaseGroupId, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Release Id", foundTrack.MusicBrainz.ReleaseId, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Album Artist Id", foundTrack.MusicBrainz.ReleaseArtistId, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Album Id", foundTrack.MusicBrainz.ReleaseId, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Album Type", foundTrack.MusicBrainz.AlbumType?.ToLower(), ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Album Release Country", foundTrack.MusicBrainz.AlbumReleaseCountry, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "MusicBrainz Album Status", foundTrack.MusicBrainz.AlbumStatus?.ToLower(), ref trackInfoUpdated);
        }
        else if (foundTrack.ProviderType == "Deezer")
        {
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Deezer Track Id", foundTrack.Id, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Deezer Track Explicit", foundTrack.Explicit ? "Y": "N", ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Deezer Track Href", foundTrack.Url, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Deezer Album Id", foundTrack.Album.Id, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Deezer Album Href", foundTrack.Album.Url, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Deezer Album Release Date", foundTrack.Album.ReleaseDate, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Deezer Artist Id", foundTrack.Album.ArtistId, ref trackInfoUpdated);
        }
        else if (foundTrack.ProviderType == "Spotify")
        {
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Spotify Track Id", foundTrack.Id, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Spotify Track Explicit", foundTrack.Explicit ? "Y": "N", ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Spotify Track Href", foundTrack.Url, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Spotify Album Id", foundTrack.Album.Id, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Spotify Album Release Date", foundTrack.Album.ReleaseDate, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Spotify Artist Id", foundTrack.Album.ArtistId, ref trackInfoUpdated);
            //_mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Spotify Artist Href", foundTrack.Album.ArtistId, ref trackInfoUpdated);
        }
        else if (foundTrack.ProviderType == "Discogs")
        {
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Discogs Track Explicit", foundTrack.Explicit ? "Y": "N", ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Discogs Album Id", foundTrack.Album.Id, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Discogs Album Release Date", foundTrack.Album.ReleaseDate, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Discogs Artist Id", foundTrack.Album.ArtistId, ref trackInfoUpdated);
            //_mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Discogs Artist Href", foundTrack.Album, ref trackInfoUpdated);
        }
        
        if (string.IsNullOrWhiteSpace(mediaFileInfo.Title) || overWriteTrack)
        {
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Title", foundTrack.Name, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(mediaFileInfo.Album) || overWriteAlbum)
        {
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Album", foundTrack.Album.Name, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(mediaFileInfo.AlbumArtist) || mediaFileInfo.AlbumArtist.ToLower().Contains("various") || overwriteAlbumArtist)
        {
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "AlbumArtist", mainArtist, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(mediaFileInfo.Artist) || mediaFileInfo.Artist.ToLower().Contains("various") || overWriteArtist)
        {
            _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Artist",  mainArtist, ref trackInfoUpdated);
        }
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "ARTISTS", artists, ref trackInfoUpdated);

        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "ISRC", foundTrack.ISRC, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "UPC", foundTrack.Album.UPC, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Date", foundTrack.Album.ReleaseDate, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Copyright", foundTrack.Copyright, ref trackInfoUpdated);
        
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Disc Number", foundTrack.DiscNumber.ToString(), ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Track Number", foundTrack.TrackNumber.ToString(), ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Total Tracks", foundTrack.Album.TotalTracks.ToString(), ref trackInfoUpdated);

        if (trackInfoUpdated)
        {
            mediaFileInfo.TaggerUpdatedTags = true;
        }
        
        return true;
    }
    
}