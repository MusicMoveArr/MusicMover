using System.Text.RegularExpressions;
using ATL;
using FuzzySharp;
using MusicMover.Helpers;
using MusicMover.Models.MetadataAPI.Entities;

namespace MusicMover.Services;

public class MiniMediaMetadataService
{
    private readonly string VariousArtists = "Various Artists";
    private readonly string VariousArtistsVA = "VA";
    private readonly MiniMediaMetadataAPIService _miniMediaMetadataApiService;
    private readonly MediaTagWriteService _mediaTagWriteService;
    private const int MatchPercentage = 80;

    private readonly List<string> _providerTypes;
    
    public MiniMediaMetadataService(string baseUrl, List<string> providerTypes)
    {
        _providerTypes = providerTypes;
        _mediaTagWriteService = new MediaTagWriteService();
        _miniMediaMetadataApiService = new MiniMediaMetadataAPIService(baseUrl, providerTypes);
    }

    public async Task<bool> WriteTagsAsync(MediaFileInfo mediaFileInfo, FileInfo fromFile, string uncoupledArtistName,
        string uncoupledAlbumArtist,
        bool overWriteArtist, bool overWriteAlbum, bool overWriteTrack, bool overwriteAlbumArtist)
    {
        if (string.IsNullOrWhiteSpace(mediaFileInfo.Artist) || string.IsNullOrWhiteSpace(mediaFileInfo.Title))
        {
            return false;
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
        
        //replace disc X from album
        string targetAlbum = mediaFileInfo.Album;
        string discPattern = "[([]{,1}[Disc|CD][ ]{0,}[0-9]{1,}[])]{,1}";
        if (Regex.IsMatch(targetAlbum, discPattern))
        {
            targetAlbum = Regex.Replace(targetAlbum, discPattern, string.Empty);
        }

        foreach (var artist in artistSearch)
        {
            Console.WriteLine($"Need to match artist: '{artist}', album: '{targetAlbum}', track: '{mediaFileInfo.Title}'");
            Console.WriteLine($"Searching for artist '{artist}'");
            
            if (await TryArtistAsync(artist, mediaFileInfo.Title, targetAlbum, fromFile, 
                    overWriteArtist, overWriteAlbum, overWriteTrack, overwriteAlbumArtist))
            {
                return true;
            }
        }

        string? artistInAlbumName = artistSearch.FirstOrDefault(artist => targetAlbum.ToLower().Contains(artist.ToLower()));
        if (!string.IsNullOrWhiteSpace(artistInAlbumName))
        {
            string withoutArtistInAlbum = targetAlbum.ToLower().Replace(artistInAlbumName.ToLower(), string.Empty);
            foreach (var artist in artistSearch)
            {
                Console.WriteLine($"Need to match artist: '{artist}', album: '{withoutArtistInAlbum}', track: '{mediaFileInfo.Title}'");
                Console.WriteLine($"Searching for artist '{artist}'");
                
                if (await TryArtistAsync(artist, mediaFileInfo.Title, withoutArtistInAlbum, fromFile, 
                        overWriteArtist, overWriteAlbum, overWriteTrack, overwriteAlbumArtist))
                {
                    return true;
                }
            }
        }
        return false;
    }
    
    private async Task<bool> TryArtistAsync(string? artistName, 
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

        var searchResult = await _miniMediaMetadataApiService.SearchArtistsAsync(artistName);

        if (!searchResult.Artists.Any())
        {
            return false;
        }

        var artists = searchResult.Artists
            .Where(artist => !string.IsNullOrWhiteSpace(artist.Name))
            .Select(artist => new
            {
                MatchedFor = Fuzz.Ratio(artistName, artist.Name),
                Artist = artist
            })
            .Where(match => FuzzyHelper.ExactNumberMatch(artistName, match.Artist.Name))
            .Where(match => match.MatchedFor >= MatchPercentage)
            .OrderByDescending(result => result.MatchedFor)
            .ThenByDescending(result => result.Artist.Popularity)
            .Select(result => result.Artist)
            .ToList();

        bool tagged = false;
        
        foreach (string provider in _providerTypes)
        {
            foreach (var artist in artists.Where(artist => artist.ProviderType == provider))
            {
                try
                {
                    SearchTrackEntity? foundTrack = await ProcessArtistAsync(artist, targetTrackTitle, targetAlbumTitle);
                    
                    if (foundTrack != null && await WriteTagsToFileAsync(foundTrack, fromFile, overWriteArtist, overWriteAlbum, overWriteTrack, overwriteAlbumArtist))
                    {
                        tagged = true;
                        break;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{e.Message}, {e.StackTrace}");
                }
            }
        }


        return tagged;
    }

    private async Task<SearchTrackEntity?> ProcessArtistAsync(SearchArtistEntity artist,
        string targetTrackTitle,
        string targetAlbumTitle)
    {
        Console.WriteLine($"MiniMedia Metadata API, search query: '{artist.Name} - {targetTrackTitle}', Provider: {artist.ProviderType}, ArtistId: '{artist.Id}'");
        var searchResultTracks = await _miniMediaMetadataApiService.SearchTracksAsync(targetTrackTitle, artist.Id, artist.ProviderType);

        List<SearchTrackEntity> matchesFound = new List<SearchTrackEntity>();
        List<SearchTrackEntity> bestTrackMatches = FindBestMatchingTracks(searchResultTracks.Tracks, targetTrackTitle, MatchPercentage);

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
                                      Fuzz.TokenSortRatio(artist.Name, artistName) > MatchPercentage) ||
                                      Fuzz.TokenSortRatio(artist.Name, string.Join(' ', artistNames)) > MatchPercentage; //maybe collab?

            if (!containsArtist)
            {
                continue;
            }
            
            if (!string.IsNullOrWhiteSpace(targetAlbumTitle) &&
                (Fuzz.Ratio(targetAlbumTitle.ToLower(), result.Album.Name.ToLower()) < MatchPercentage ||
                 !FuzzyHelper.ExactNumberMatch(targetAlbumTitle, result.Album.Name)))
            {
                continue;
            }

            var trackMatches = FindBestMatchingTracks([result], targetTrackTitle, MatchPercentage);

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
                TitleMatchedFor = Fuzz.Ratio(targetTrackTitle.ToLower(), match.Name.ToLower()),
                AlbumMatchedFor = Fuzz.Ratio(targetAlbumTitle.ToLower(), match.Album.Name.ToLower()),
                Match = match
            })
            .OrderByDescending(result => result.TitleMatchedFor)
            .ThenByDescending(result => result.AlbumMatchedFor)
            .ToList();
        
        //Console.WriteLine("Tracks to match with:");
        //Console.WriteLine($"Match with, Title '{targetTrackTitle}', Album '{targetAlbumTitle}', Artist: '{artist.Name}'");
        //foreach (var match in searchResultTracks.Tracks)
        //{
        //    string? mainArtist = match.Artists.FirstOrDefault(artist => artist.Id == match.Album.ArtistId)?.Name;
        //    Console.WriteLine($"Title '{match.Name}', Album '{match.Album.Name}', Artist: '{mainArtist}'");
        //}
        
        Console.WriteLine("Matches:");
        foreach (var match in bestMatches)
        {
            Console.WriteLine($"Title '{match.Match.Name}' matched for {match.TitleMatchedFor}%, Album '{match.Match.Album.Name}' matched for {match.AlbumMatchedFor}%");
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
                TitleMatchedFor = Fuzz.Ratio(targetTrackTitle?.ToLower(), t.Name.ToLower()),
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

        if (Fuzz.Ratio(name, VariousArtists) >= 95)
        {
            return true;
        }

        return false;
    }
    
    private async Task<bool> WriteTagsToFileAsync(SearchTrackEntity foundTrack, FileInfo fromFile,
        bool overWriteArtist, bool overWriteAlbum, bool overWriteTrack, bool overwriteAlbumArtist)
    {
        Track track = new Track(fromFile.FullName);
        bool trackInfoUpdated = false;
        string artists = string.Join(';', foundTrack.Artists.Select(artist => artist.Name));

        string? mainArtist = foundTrack.Artists.FirstOrDefault(artist => artist.Id == foundTrack.Album.ArtistId)?.Name;

        if (string.IsNullOrWhiteSpace(mainArtist))
        {
            Console.WriteLine("Main artist is missing, bug in MiniMedia's Metadata API?");
            return false;
        }
        
        Console.WriteLine($"Filpath: {fromFile.FullName}");
        Console.WriteLine($"API Artist: {mainArtist}");
        Console.WriteLine($"API Album: {foundTrack.Album.Name}");
        Console.WriteLine($"API TrackName: {foundTrack.Name}");
        Console.WriteLine($"Media Artist: {track.Artist}");
        Console.WriteLine($"Media AlbumArtist: {track.AlbumArtist}");
        Console.WriteLine($"Media Album: {track.Album}");
        Console.WriteLine($"Media TrackName: {track.Title}");

        if (foundTrack.ProviderType == "Tidal")
        {
            UpdateTag(track, "Tidal Track Id", foundTrack.Id, ref trackInfoUpdated);
            UpdateTag(track, "Tidal Track Explicit", foundTrack.Explicit ? "Y": "N", ref trackInfoUpdated);
            UpdateTag(track, "Tidal Track Href", foundTrack.Url, ref trackInfoUpdated);
            UpdateTag(track, "Tidal Album Id", foundTrack.Album.Id, ref trackInfoUpdated);
            UpdateTag(track, "Tidal Album Href", foundTrack.Album.Url, ref trackInfoUpdated);
            UpdateTag(track, "Tidal Album Release Date", foundTrack.Album.ReleaseDate, ref trackInfoUpdated);
            UpdateTag(track, "Tidal Artist Id", foundTrack.Album.ArtistId, ref trackInfoUpdated);
        }
        else if (foundTrack.ProviderType == "MusicBrainz")
        {
            UpdateTag(track, "MusicBrainz Artist Id", foundTrack.MusicBrainz.ArtistId, ref trackInfoUpdated);
            UpdateTag(track, "MusicBrainz Track Id", foundTrack.MusicBrainz.RecordingId, ref trackInfoUpdated);
            UpdateTag(track, "MusicBrainz Release Track Id", foundTrack.MusicBrainz.ReleaseTrackId, ref trackInfoUpdated);
            UpdateTag(track, "MusicBrainz Release Artist Id", foundTrack.MusicBrainz.ReleaseArtistId, ref trackInfoUpdated);
            UpdateTag(track, "MusicBrainz Release Group Id", foundTrack.MusicBrainz.ReleaseGroupId, ref trackInfoUpdated);
            UpdateTag(track, "MusicBrainz Release Id", foundTrack.MusicBrainz.ReleaseId, ref trackInfoUpdated);
            UpdateTag(track, "MusicBrainz Album Artist Id", foundTrack.MusicBrainz.ReleaseArtistId, ref trackInfoUpdated);
            UpdateTag(track, "MusicBrainz Album Id", foundTrack.MusicBrainz.ReleaseId, ref trackInfoUpdated);
            UpdateTag(track, "MusicBrainz Album Type", foundTrack.MusicBrainz.AlbumType?.ToLower(), ref trackInfoUpdated);
            UpdateTag(track, "MusicBrainz Album Release Country", foundTrack.MusicBrainz.AlbumReleaseCountry, ref trackInfoUpdated);
            UpdateTag(track, "MusicBrainz Album Status", foundTrack.MusicBrainz.AlbumStatus?.ToLower(), ref trackInfoUpdated);
        }
        else if (foundTrack.ProviderType == "Deezer")
        {
            UpdateTag(track, "Deezer Track Id", foundTrack.Id, ref trackInfoUpdated);
            UpdateTag(track, "Deezer Track Explicit", foundTrack.Explicit ? "Y": "N", ref trackInfoUpdated);
            UpdateTag(track, "Deezer Track Href", foundTrack.Url, ref trackInfoUpdated);
            UpdateTag(track, "Deezer Album Id", foundTrack.Album.Id, ref trackInfoUpdated);
            UpdateTag(track, "Deezer Album Href", foundTrack.Album.Url, ref trackInfoUpdated);
            UpdateTag(track, "Deezer Album Release Date", foundTrack.Album.ReleaseDate, ref trackInfoUpdated);
            UpdateTag(track, "Deezer Artist Id", foundTrack.Album.ArtistId, ref trackInfoUpdated);
        }
        else if (foundTrack.ProviderType == "Spotify")
        {
            UpdateTag(track, "Spotify Track Id", foundTrack.Id, ref trackInfoUpdated);
            UpdateTag(track, "Spotify Track Explicit", foundTrack.Explicit ? "Y": "N", ref trackInfoUpdated);
            UpdateTag(track, "Spotify Track Href", foundTrack.Url, ref trackInfoUpdated);
            UpdateTag(track, "Spotify Album Id", foundTrack.Album.Id, ref trackInfoUpdated);
            UpdateTag(track, "Spotify Album Release Date", foundTrack.Album.ReleaseDate, ref trackInfoUpdated);
            UpdateTag(track, "Spotify Artist Id", foundTrack.Album.ArtistId, ref trackInfoUpdated);
            UpdateTag(track, "Spotify Artist Href", foundTrack.Album.ArtistId, ref trackInfoUpdated);
        }
        
        if (string.IsNullOrWhiteSpace(track.Title) || overWriteTrack)
        {
            UpdateTag(track, "Title", foundTrack.Name, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.Album) || overWriteAlbum)
        {
            UpdateTag(track, "Album", foundTrack.Album.Name, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.AlbumArtist) || track.AlbumArtist.ToLower().Contains("various") || overwriteAlbumArtist)
        {
            UpdateTag(track, "AlbumArtist", mainArtist, ref trackInfoUpdated);
        }
        if (string.IsNullOrWhiteSpace(track.Artist) || track.Artist.ToLower().Contains("various") || overWriteArtist)
        {
            UpdateTag(track, "Artist",  mainArtist, ref trackInfoUpdated);
        }
        UpdateTag(track, "ARTISTS", artists, ref trackInfoUpdated);

        UpdateTag(track, "ISRC", foundTrack.ISRC, ref trackInfoUpdated);
        UpdateTag(track, "UPC", foundTrack.Album.UPC, ref trackInfoUpdated);
        UpdateTag(track, "Date", foundTrack.Album.ReleaseDate, ref trackInfoUpdated);
        UpdateTag(track, "Copyright", foundTrack.Copyright, ref trackInfoUpdated);
        
        UpdateTag(track, "Disc Number", foundTrack.DiscNumber.ToString(), ref trackInfoUpdated);
        UpdateTag(track, "Track Number", foundTrack.TrackNumber.ToString(), ref trackInfoUpdated);
        UpdateTag(track, "Total Tracks", foundTrack.Album.TotalTracks.ToString(), ref trackInfoUpdated);

        return await _mediaTagWriteService.SafeSaveAsync(track);
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

        if (tempIsUpdated&& !string.Equals(orgValue, value))
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