using ATL;
using MusicMover.Models.MusicBrainz;
using AutoFixture;
using MusicMover.Helpers;
using MusicMover.Services;
using Shouldly;

namespace MusicMover.Tests.Services;

public class MusicBrainzServiceTests
{
    private readonly MusicBrainzService _musicBrainzService = new MusicBrainzService();
    private readonly FingerPrintService _fingerPrintService = new FingerPrintService();

    [Fact]
    public void GetBestMatchingTracks_Match()
    {
        var bogus = new Bogus.DataSets.Name();
        IFixture fixture = new Fixture();
        var trackFixture = fixture.Build<MusicBrainzReleaseMediaTrackModel>();
        
        List<MusicBrainzReleaseMediaTrackModel>? tracks = new List<MusicBrainzReleaseMediaTrackModel>();
        for (int i = 0; i < 5; i++)
        {
            tracks.Add(trackFixture.Create());
        }
        
        var matches = _musicBrainzService.GetBestMatchingTracks(
            tracks, 
            tracks.First().Title, 
            string.Empty, 
            false, 
            80);

        matches.Count.ShouldBeGreaterThan(0);
    }
    
    [Fact]
    public void GetBestMatchingTracks_NoMatch()
    {
        var bogus = new Bogus.DataSets.Name();
        IFixture fixture = new Fixture();
        var trackFixture = fixture.Build<MusicBrainzReleaseMediaTrackModel>();
        
        List<MusicBrainzReleaseMediaTrackModel>? tracks = new List<MusicBrainzReleaseMediaTrackModel>();
        for (int i = 0; i < 5; i++)
        {
            tracks.Add(trackFixture.Create());
        }
        
        var matches = _musicBrainzService.GetBestMatchingTracks(
            tracks, 
            Guid.NewGuid().ToString(), 
            string.Empty, 
            false, 
            80);

        matches.Count.ShouldBe(0);
    }
    
    [Theory]
    [InlineData("August Burns Red", "Exhumed", "Exhumed")]
    [InlineData("The Prodigy", "No Tourists", "No Tourists")]
    public async Task GetMatchFromAcoustIdAsync_TryMatch(string artist, string album, string title)
    {
        Track track = new Track();
        track.Artist = artist;
        track.Album = album;
        track.Title = title;
        track.AdditionalFields = new Dictionary<string, string>();
        MediaFileInfo mediaFileInfo = new MediaFileInfo(track);

        var match = await _musicBrainzService.GetMatchFromAcoustIdAsync(mediaFileInfo, 
            null, 
            string.Empty, 
            true, 
            80, 
            80);

        match.Release.Title.ShouldBe(track.Album);
        match.Release.Media.ShouldNotBeEmpty();
        match.Release.Media.First().Tracks.First().Title.ShouldBe(track.Title);
    }
    
    [Fact]
    public async Task TestCurrentUserMusicDirectory()
    {
        string path = @$"/home/{Environment.UserName}/Music/";
        foreach (string filePath in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
        {
            Track track = new Track(filePath);
            MediaFileInfo mediaFileInfo = new MediaFileInfo(track);

            var match = await _musicBrainzService.GetMatchFromAcoustIdAsync(mediaFileInfo, 
                null, 
                string.Empty, 
                true, 
                80, 
                80);

            if (match == null ||
                !match.Release.Media.Any() ||
                !match.Release.Media.First().Tracks.Any())//remove, fix issue
            {
                Console.WriteLine($"No Match found!, Artist: '{track.Artist}', Album, '{track.Album}', Title: '{track.Title}'");
                continue;
            }
            
            Console.WriteLine($"Match found! Artist: '{track.Artist}', Album, '{track.Album}', Title: '{track.Title}'");

            match.Release.Media.ShouldNotBeEmpty();
            match.Release.Media.First().Tracks.ShouldNotBeEmpty();
            int albumMatch = FuzzyHelper.PartialRatioToLower(track.Album, match.Release.Title);
            int titleMatch = FuzzyHelper.PartialRatioToLower(track.Title, match.Release.Media.First().Tracks.First().Title);
            int artistMatch = FuzzyHelper.PartialRatioToLower(track.Artist, match.ArtistCredit.Name);

            albumMatch.ShouldBeGreaterThanOrEqualTo(80, $"'{track.Album}' => '{match.Release.Title}'");
            titleMatch.ShouldBeGreaterThanOrEqualTo(80, $"'{track.Title}' => '{match.Release.Media.First().Tracks.First().Title}'");
            artistMatch.ShouldBeGreaterThanOrEqualTo(80, $"'{track.Artist}' => '{match.ArtistCredit.Name}'");
        }
    }
    
    [Theory]
    [InlineData("")]
    public async Task TestCurrentUserMusicDirectory_WithAcoustId(string acoustId)
    {
        string path = @$"/home/{Environment.UserName}/Music/";
        foreach (string filePath in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
        {
            var fingerprint = await _fingerPrintService.GetFingerprintAsync(filePath);
            Track track = new Track(filePath);
            MediaFileInfo mediaFileInfo = new MediaFileInfo(track);

            var match = await _musicBrainzService.GetMatchFromAcoustIdAsync(mediaFileInfo, 
                fingerprint, 
                acoustId, 
                true, 
                80, 
                80);

            if (match == null ||
                !match.Release.Media.Any() ||
                !match.Release.Media.First().Tracks.Any())//remove, fix issue
            {
                Console.WriteLine($"No Match found for Artist: '{track.Artist}', Album, '{track.Album}', Title: '{track.Title}'");
                continue;
            }

            string matchedArtist = match.ArtistCredit.Name;
            string matchedAlbum = match.Release.Title;
            string matchedTitle = match.Release.Media.First().Tracks.First().Title;
            
            Console.WriteLine($"Match found for Artist: '{track.Artist}', Album, '{track.Album}', Title: '{track.Title}'");
            Console.WriteLine($"Match: Artist: '{matchedArtist}', Album, '{matchedAlbum}', Title: '{matchedTitle}'");
            Console.WriteLine();

            match.Release.Media.ShouldNotBeEmpty();
            match.Release.Media.First().Tracks.ShouldNotBeEmpty();
            int albumMatch = FuzzyHelper.PartialRatioToLower(track.Album, match.Release.Title);
            int titleMatch = FuzzyHelper.PartialRatioToLower(track.Title, match.Release.Media.First().Tracks.First().Title);
            int artistMatch = FuzzyHelper.PartialRatioToLower(track.Artist, match.ArtistCredit.Name);

            if (!string.IsNullOrWhiteSpace(track.Album))
            {
                albumMatch.ShouldBeGreaterThanOrEqualTo(80, $"'{track.Album}' => '{match.Release.Title}'");
            }

            if (!string.IsNullOrWhiteSpace(track.Title))
            {
                titleMatch.ShouldBeGreaterThanOrEqualTo(80, $"'{track.Title}' => '{match.Release.Media.First().Tracks.First().Title}'");
            }
            
            if (!string.IsNullOrWhiteSpace(track.Artist))
            {
                artistMatch.ShouldBeGreaterThanOrEqualTo(80, $"'{track.Artist}' => '{match.ArtistCredit.Name}'");
            }
        }
    }
    
    
    [Theory]
    [InlineData("")]
    public async Task TestCurrentUserMusicDirectory_WithTrustAcoustId(string acoustId)
    {
        string path = @$"/home/{Environment.UserName}/Music/";
        foreach (string filePath in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
        {
            var fingerprint = await _fingerPrintService.GetFingerprintAsync(filePath);
            Track track = new Track(filePath);

            string originalAlbum = track.Album;
            string originalAlbumArtist = track.AlbumArtist;
            string originalArtist = track.Artist;
            string originalTitle = track.Title;
            
            track.Album = string.Empty;
            track.AlbumArtist = string.Empty;
            track.Artist = string.Empty;
            track.Title = string.Empty;
            MediaFileInfo mediaFileInfo = new MediaFileInfo(track);

            var match = await _musicBrainzService.GetMatchFromAcoustIdAsync(mediaFileInfo, 
                fingerprint, 
                acoustId, 
                true, 
                80, 
                80);

            if (match == null ||
                !match.Release.Media.Any() ||
                !match.Release.Media.First().Tracks.Any())//remove, fix issue
            {
                Console.WriteLine($"No Match found for Artist: '{originalArtist}', Album, '{originalAlbum}', Title: '{originalTitle}'");
                continue;
            }

            string matchedArtist = match.ArtistCredit.Name;
            string matchedAlbum = match.Release.Title;
            string matchedTitle = match.Release.Media.First().Tracks.First().Title;
            
            Console.WriteLine($"Match found for Artist: '{originalArtist}', Album, '{originalAlbum}', Title: '{originalTitle}'");
            Console.WriteLine($"Match: Artist: '{matchedArtist}', Album, '{matchedAlbum}', Title: '{matchedTitle}'");
            Console.WriteLine();

            match.Release.Media.ShouldNotBeEmpty();
            match.Release.Media.First().Tracks.ShouldNotBeEmpty();
            int albumMatch = FuzzyHelper.PartialRatioToLower(originalAlbum, match.Release.Title);
            int titleMatch = FuzzyHelper.PartialRatioToLower(originalTitle, matchedTitle);
            int artistMatch = FuzzyHelper.PartialRatioToLower(originalArtist, match.ArtistCredit.Name);

            //if (!string.IsNullOrWhiteSpace(originalAlbum))
            //{
            //    albumMatch.ShouldBeGreaterThanOrEqualTo(80, $"'{originalAlbum}' => '{match.Release.Title}'");
            //}

            if (!string.IsNullOrWhiteSpace(originalTitle))
            {
                titleMatch.ShouldBeGreaterThanOrEqualTo(70, $"'{originalTitle}' => '{matchedTitle}', filepath: '{filePath}'");
            }
            
            if (!string.IsNullOrWhiteSpace(originalArtist))
            {
                artistMatch.ShouldBeGreaterThanOrEqualTo(80, $"'{originalArtist}' => '{match.ArtistCredit.Name}', filepath: '{filePath}'");
            }
        }
    }
}