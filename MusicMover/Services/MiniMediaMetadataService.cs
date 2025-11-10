using System.Text.RegularExpressions;
using MusicMover.Helpers;
using MusicMover.MediaHandlers;
using MusicMover.Models.MetadataAPI;
using MusicMover.Models.MetadataAPI.Entities;

namespace MusicMover.Services;

public class MiniMediaMetadataService
{
    private readonly string VariousArtists = "Various Artists";
    private readonly string VariousArtistsVA = "VA";
    private readonly MiniMediaMetadataApiCacheLayerService _miniMediaMetadataApiCacheLayerService;
    private readonly MediaTagWriteService _mediaTagWriteService;

    private readonly List<string> _providerTypes;
    
    public MiniMediaMetadataService(string baseUrl, List<string> providerTypes)
    {
        _providerTypes = providerTypes;
        _mediaTagWriteService = new MediaTagWriteService();
        _miniMediaMetadataApiCacheLayerService = new MiniMediaMetadataApiCacheLayerService(baseUrl, providerTypes);
    }

    public async Task<List<SearchTrackEntity>> GetMatchesAsync(
        MediaHandler mediaHandler,
        int matchPercentage)
    {
        if (string.IsNullOrWhiteSpace(mediaHandler.Artist) || string.IsNullOrWhiteSpace(mediaHandler.Title))
        {
            return new List<SearchTrackEntity>();
        }

        if (!mediaHandler.AllArtistNames.Any())
        {
            return new List<SearchTrackEntity>();
        }
        
        //replace disc X from album
        string targetAlbum = mediaHandler?.Album ?? string.Empty;
        string discPattern = @"(?:[\[(])?(disc|cd)\s*([0-9]+)[\])]?";
        if (Regex.IsMatch(targetAlbum.ToLower(), discPattern))
        {
            targetAlbum = Regex.Replace(targetAlbum.ToLower(), discPattern, string.Empty).TrimEnd();
        }

        string tag_Url = mediaHandler.GetMediaTagValue("url");

        if (!string.IsNullOrWhiteSpace(tag_Url) && 
            tag_Url.StartsWith("https://tidal.com/browse/track/") &&
            int.TryParse(tag_Url.Split('/').LastOrDefault(), out int tidalTrackId))
        {
            Logger.WriteLine($"MiniMedia Metadata API, get track query: '{tidalTrackId}', Provider: Tidal", true);
            var searchResultTracks = await _miniMediaMetadataApiCacheLayerService.GetTrackByIdAsync(tidalTrackId.ToString(), "Tidal");
            
            SearchTrackEntity? foundTrack = await ProcessTracksAsync(
                mediaHandler.AllArtistNames, 
                searchResultTracks, 
                mediaHandler.Title, 
                mediaHandler.Album, 
                matchPercentage);
            
            if (foundTrack != null)
            {
                return [foundTrack];
            }
        }

        foreach (var artist in mediaHandler.AllArtistNames)
        {
            Logger.WriteLine($"Need to match artist: '{artist}', album: '{targetAlbum}', track: '{mediaHandler.Title}'", true);
            Logger.WriteLine($"Searching for artist '{artist}'", true);

            List<SearchTrackEntity> foundTracks = await TryArtistAsync(artist, mediaHandler.Title, targetAlbum, matchPercentage);
            if (foundTracks.Count > 0)
            {
                return foundTracks;
            }
        }

        string? artistInAlbumName = mediaHandler.AllArtistNames.FirstOrDefault(artist => targetAlbum.ToLower().Contains(artist.ToLower()));
        if (!string.IsNullOrWhiteSpace(artistInAlbumName))
        {
            string withoutArtistInAlbum = targetAlbum.ToLower().Replace(artistInAlbumName.ToLower(), string.Empty);
            foreach (var artist in mediaHandler.AllArtistNames)
            {
                Logger.WriteLine($"Need to match artist: '{artist}', album: '{withoutArtistInAlbum}', track: '{mediaHandler.Title}'", true);
                Logger.WriteLine($"Searching for artist '{artist}'", true);

                List<SearchTrackEntity> foundTracks = await TryArtistAsync(artist, mediaHandler.Title, withoutArtistInAlbum, matchPercentage);
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

        var searchResult = await _miniMediaMetadataApiCacheLayerService.SearchArtistsAsync(artistName);

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
                    MatchedFor = Math.Max(FuzzyHelper.FuzzRatioToLower(artistName, artist.Name), 
                                          !string.IsNullOrWhiteSpace(artist.MusicBrainz.SortName) ? 
                                              FuzzyHelper.FuzzTokenSortRatioToLower(artistName, artist.MusicBrainz.SortName) : 0),
                    Artist = artist
                })
                .Where(match => FuzzyHelper.ExactNumberMatch(artistName, match.Artist.Name) ||
                                                    (!string.IsNullOrWhiteSpace(match.Artist.MusicBrainz.SortName) ? 
                                                        FuzzyHelper.ExactNumberMatch(artistName, match.Artist.MusicBrainz.SortName) : false))
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
                    Logger.WriteLine($"MiniMedia Metadata API, search query: '{artist.Name} - {targetTrackTitle}', Provider: {artist.ProviderType}, ArtistId: '{artist.Id}'", true);
                    var searchResultTracks = await _miniMediaMetadataApiCacheLayerService.SearchTracksAsync(targetTrackTitle, artist.Id, artist.ProviderType);

                    SearchTrackEntity? foundTrack = await ProcessTracksAsync([artist.Name], searchResultTracks, targetTrackTitle, targetAlbumTitle, matchPercentage);
                    
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

    private async Task<SearchTrackEntity?> ProcessTracksAsync(
        List<string> targetArtistNames,
        SearchTrackResponse searchTrackResponse,
        string targetTrackTitle,
        string targetAlbumTitle,
        int matchPercentage)
    {
        
        List<SearchTrackEntity> matchesFound = new List<SearchTrackEntity>();
        List<SearchTrackEntity> bestTrackMatches = FindBestMatchingTracks(searchTrackResponse.Tracks, targetTrackTitle, matchPercentage);

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
            
            artistNames.AddRange(result.Artists
                .Where(artist => !string.IsNullOrWhiteSpace(artist.MusicBrainz.SortName))
                .Select(artist => artist.MusicBrainz.SortName)
                .ToList());

            artistNames = artistNames
                .Distinct()
                .ToList();
                
            bool containsArtist = artistNames.Any(artistName => 
                                      targetArtistNames.Any(targetArtist => 
                                          FuzzyHelper.FuzzTokenSortRatioToLower(targetArtist, artistName) > matchPercentage)) ||
                                  
                                  targetArtistNames.Any(targetArtist => 
                                      FuzzyHelper.FuzzTokenSortRatioToLower(targetArtist, string.Join(' ', artistNames)) > matchPercentage); //maybe collab?
            
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
        MediaHandler mediaHandler,
        bool overWriteArtist, 
        bool overWriteAlbum, 
        bool overWriteTrack, 
        bool overwriteAlbumArtist)
    {
        bool trackInfoUpdated = false;
        string artists = string.Join(';', foundTrack.Artists.Select(artist => artist.Name));

        string? mainArtist = foundTrack.Artists.FirstOrDefault(artist => artist.Id == foundTrack.Album.ArtistId)?.Name;

        if (string.IsNullOrWhiteSpace(mainArtist) && 
            !string.IsNullOrWhiteSpace(foundTrack.Album.ArtistId))
        {
            var artistResponse = await _miniMediaMetadataApiCacheLayerService
                .GetArtistByIdAsync(foundTrack.Album.ArtistId, foundTrack.ProviderType);

            mainArtist = artistResponse?.Artists?.FirstOrDefault(artist => artist.Id == foundTrack.Album.ArtistId)?.Name;
        }
        
        if (string.IsNullOrWhiteSpace(mainArtist))
        {
            Logger.WriteLine("Main artist is missing, bug in MiniMedia's Metadata API?");
            return false;
        }

        mainArtist = ArtistHelper.GetUncoupledArtistName(mainArtist);
        
        Logger.WriteLine($"Filpath: {mediaHandler.FileInfo.FullName}", true);
        Logger.WriteLine($"Provider: {foundTrack.ProviderType}", true);
        Logger.WriteLine($"API Artist: {mainArtist}", true);
        Logger.WriteLine($"API Album: {foundTrack.Album.Name}", true);
        Logger.WriteLine($"API TrackName: {foundTrack.Name}", true);
        Logger.WriteLine($"Media Artist: {mediaHandler.Artist}", true);
        Logger.WriteLine($"Media AlbumArtist: {mediaHandler.AlbumArtist}", true);
        Logger.WriteLine($"Media Album: {mediaHandler.Album}", true);
        Logger.WriteLine($"Media TrackName: {mediaHandler.Title}", true);

        if (foundTrack.ProviderType == "Tidal")
        {
            _mediaTagWriteService.UpdateTag(mediaHandler, "Tidal Track Id", foundTrack.Id, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Tidal Track Explicit", foundTrack.Explicit ? "Y": "N", ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Tidal Track Href", foundTrack.Url, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Tidal Album Id", foundTrack.Album.Id, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Tidal Album Href", foundTrack.Album.Url, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Tidal Album Release Date", foundTrack.Album.ReleaseDate, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Tidal Artist Id", foundTrack.Album.ArtistId, ref trackInfoUpdated);
        }
        else if (foundTrack.ProviderType == "MusicBrainz")
        {
            _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Artist Id", foundTrack.MusicBrainz.ArtistId, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Track Id", foundTrack.MusicBrainz.RecordingId, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Release Track Id", foundTrack.MusicBrainz.ReleaseTrackId, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Release Artist Id", foundTrack.MusicBrainz.ReleaseArtistId, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Release Group Id", foundTrack.MusicBrainz.ReleaseGroupId, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Release Id", foundTrack.MusicBrainz.ReleaseId, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Album Artist Id", foundTrack.MusicBrainz.ReleaseArtistId, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Album Id", foundTrack.MusicBrainz.ReleaseId, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Album Type", foundTrack.MusicBrainz.AlbumType?.ToLower(), ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Album Release Country", foundTrack.MusicBrainz.AlbumReleaseCountry, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "MusicBrainz Album Status", foundTrack.MusicBrainz.AlbumStatus?.ToLower(), ref trackInfoUpdated);
        }
        else if (foundTrack.ProviderType == "Deezer")
        {
            _mediaTagWriteService.UpdateTag(mediaHandler, "Deezer Track Id", foundTrack.Id, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Deezer Track Explicit", foundTrack.Explicit ? "Y": "N", ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Deezer Track Href", foundTrack.Url, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Deezer Album Id", foundTrack.Album.Id, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Deezer Album Href", foundTrack.Album.Url, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Deezer Album Release Date", foundTrack.Album.ReleaseDate, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Deezer Artist Id", foundTrack.Album.ArtistId, ref trackInfoUpdated);
        }
        else if (foundTrack.ProviderType == "Spotify")
        {
            _mediaTagWriteService.UpdateTag(mediaHandler, "Spotify Track Id", foundTrack.Id, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Spotify Track Explicit", foundTrack.Explicit ? "Y": "N", ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Spotify Track Href", foundTrack.Url, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Spotify Album Id", foundTrack.Album.Id, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Spotify Album Release Date", foundTrack.Album.ReleaseDate, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Spotify Artist Id", foundTrack.Album.ArtistId, ref trackInfoUpdated);
            //_mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Spotify Artist Href", foundTrack.Album.ArtistId, ref trackInfoUpdated);
        }
        else if (foundTrack.ProviderType == "Discogs")
        {
            _mediaTagWriteService.UpdateTag(mediaHandler, "Discogs Track Explicit", foundTrack.Explicit ? "Y": "N", ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Discogs Album Id", foundTrack.Album.Id, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Discogs Album Release Date", foundTrack.Album.ReleaseDate, ref trackInfoUpdated);
            _mediaTagWriteService.UpdateTag(mediaHandler, "Discogs Artist Id", foundTrack.Album.ArtistId, ref trackInfoUpdated);
            //_mediaTagWriteService.UpdateTag(mediaFileInfo.TrackInfo, "Discogs Artist Href", foundTrack.Album, ref trackInfoUpdated);
        }
        
        if (string.IsNullOrWhiteSpace(mediaHandler.Title) || overWriteTrack)
        {
            _mediaTagWriteService.UpdateTag(mediaHandler, "Title", foundTrack.Name, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(mediaHandler.Album) || overWriteAlbum)
        {
            _mediaTagWriteService.UpdateTag(mediaHandler, "Album", foundTrack.Album.Name, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(mediaHandler.AlbumArtist) || mediaHandler.AlbumArtist.ToLower().Contains("various") || overwriteAlbumArtist)
        {
            _mediaTagWriteService.UpdateTag(mediaHandler, "AlbumArtist", mainArtist, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(mediaHandler.Artist) || mediaHandler.Artist.ToLower().Contains("various") || overWriteArtist)
        {
            _mediaTagWriteService.UpdateTag(mediaHandler, "Artist",  mainArtist, ref trackInfoUpdated);
        }
        _mediaTagWriteService.UpdateTag(mediaHandler, "ARTISTS", artists, ref trackInfoUpdated);

        _mediaTagWriteService.UpdateTag(mediaHandler, "ISRC", foundTrack.ISRC, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "UPC", foundTrack.Album.UPC, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "Date", foundTrack.Album.ReleaseDate, ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "Copyright", foundTrack.Copyright, ref trackInfoUpdated);
        
        _mediaTagWriteService.UpdateTag(mediaHandler, "Disc Number", foundTrack.DiscNumber.ToString(), ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "Track Number", foundTrack.TrackNumber.ToString(), ref trackInfoUpdated);
        _mediaTagWriteService.UpdateTag(mediaHandler, "Total Tracks", foundTrack.Album.TotalTracks.ToString(), ref trackInfoUpdated);

        if (trackInfoUpdated)
        {
            mediaHandler.TaggerUpdatedTags = true;
        }
        
        return true;
    }
    
}