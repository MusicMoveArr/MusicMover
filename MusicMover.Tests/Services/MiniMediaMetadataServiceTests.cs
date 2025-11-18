using ATL;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using MusicMover.Helpers;
using MusicMover.MediaHandlers;
using MusicMover.Services;
using Shouldly;

namespace MusicMover.Tests.Services;

public class MiniMediaMetadataServiceTests
{
    [Theory]
    [InlineData("")]
    public async Task TestCurrentUserMusicDirectory(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new TestCanceledException("baseUrl is required");
        }
        
        List<string> providerTypes = ["Any"];
        MiniMediaMetadataService miniMediaMetadataService = new MiniMediaMetadataService(baseUrl, providerTypes, new TranslationService(string.Empty));
        string path = @$"/home/{Environment.UserName}/Music/";
        
        foreach (string filePath in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
        {
            Track track = new Track(filePath);
            MediaHandlerDummy mediaHandler = new MediaHandlerDummy();
            mediaHandler.SetMediaTagValue(track.Artist, nameof(track.Artist));
            mediaHandler.SetMediaTagValue(track.AlbumArtist, nameof(track.AlbumArtist));
            mediaHandler.SetMediaTagValue(track.Album, nameof(track.Album));
            mediaHandler.SetMediaTagValue(track.Title, nameof(track.Title));
            mediaHandler.SetMediaTagValue(track.DiscNumber, nameof(track.DiscNumber));
            mediaHandler.SetMediaTagValue(track.TrackNumber, nameof(track.TrackNumber));
            mediaHandler.SetArtists();

            string uncoupledArtistName = ArtistHelper.GetUncoupledArtistName(track.Artist);
            string uncoupledAlbumArtistName = ArtistHelper.GetUncoupledArtistName(track.AlbumArtist);

            var matches = await miniMediaMetadataService.GetMatchesAsync(
                mediaHandler, 
                80);
            
            if (!matches.Any())
            {
                Console.WriteLine($"No Match found!, Artist: '{track.Artist}', Album, '{track.Album}', Title: '{track.Title}'");
                continue;
            }
            
            Console.WriteLine($"Match found! Artist: '{track.Artist}', Album, '{track.Album}', Title: '{track.Title}'");

            foreach (var match in matches)
            {
                int albumMatch = FuzzyHelper.PartialRatioToLower(track.Album, match.Album.Name);
                int titleMatch = FuzzyHelper.PartialRatioToLower(track.Title, match.Name);
                int artistMatch = 0;

                foreach (var artist in match.Artists)
                {
                    int tempArtistMatch = FuzzyHelper.PartialRatioToLower(track.Artist, artist.Name);
                    if (tempArtistMatch > artistMatch)
                    {
                        artistMatch = tempArtistMatch;
                    }
                }

                albumMatch.ShouldBeGreaterThanOrEqualTo(80, $"'{track.Album}' => '{match.Album.Name}'");
                titleMatch.ShouldBeGreaterThanOrEqualTo(80, $"'{track.Title}' => '{match.Name}'");
                artistMatch.ShouldBeGreaterThanOrEqualTo(80, $"'{track.Artist}' => '{match.Artists.First().Name}'");
            }
        }
    }
    
    
    [Theory]
    [InlineData("", new [] { "Any" }, "Tokyo Machine", "Various Artists", "Monstercat – Best of 2016", "FIGHT")]
    public async Task TestSpecificTracks_Ok(
        string baseUrl, 
        string[] providerTypes, 
        string artist, 
        string albumArtist, 
        string album, 
        string title)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new TestCanceledException("baseUrl is required");
        }
        
        MiniMediaMetadataService miniMediaMetadataService = new MiniMediaMetadataService(baseUrl, providerTypes.ToList(), new TranslationService(string.Empty));
        
        Track track = new Track();
        track.Artist = artist;
        track.AlbumArtist = albumArtist;
        track.Album = album;
        track.Title = title;
        MediaHandlerDummy mediaHandler = new MediaHandlerDummy();
        mediaHandler.SetMediaTagValue(track.Artist, nameof(track.Artist));
        mediaHandler.SetMediaTagValue(track.AlbumArtist, nameof(track.AlbumArtist));
        mediaHandler.SetMediaTagValue(track.Album, nameof(track.Album));
        mediaHandler.SetMediaTagValue(track.Title, nameof(track.Title));
        mediaHandler.SetMediaTagValue(track.DiscNumber, nameof(track.DiscNumber));
        mediaHandler.SetMediaTagValue(track.TrackNumber, nameof(track.TrackNumber));
        mediaHandler.SetArtists();

        var matches = await miniMediaMetadataService.GetMatchesAsync(
            mediaHandler, 
            80);
        
        if (!matches.Any())
        {
            Console.WriteLine($"No Match found!, Artist: '{track.Artist}', Album, '{track.Album}', Title: '{track.Title}'");
            return;
        }
        
        Console.WriteLine($"Match found! Artist: '{track.Artist}', Album, '{track.Album}', Title: '{track.Title}'");

        foreach (var match in matches)
        {
            int albumMatch = FuzzyHelper.PartialRatioToLower(track.Album, match.Album.Name);
            int titleMatch = FuzzyHelper.PartialRatioToLower(track.Title, match.Name);
            int artistMatch = 0;

            foreach (var artistz in match.Artists)
            {
                int tempArtistMatch = FuzzyHelper.PartialRatioToLower(track.Artist, artistz.Name);
                if (tempArtistMatch > artistMatch)
                {
                    artistMatch = tempArtistMatch;
                }
            }

            albumMatch.ShouldBeGreaterThanOrEqualTo(80, $"'{track.Album}' => '{match.Album.Name}'");
            titleMatch.ShouldBeGreaterThanOrEqualTo(80, $"'{track.Title}' => '{match.Name}'");
            artistMatch.ShouldBeGreaterThanOrEqualTo(80, $"'{track.Artist}' => '{match.Artists.First().Name}'");
        }
    }
}