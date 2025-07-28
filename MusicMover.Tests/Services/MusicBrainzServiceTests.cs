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
        foreach (string filePath in Directory.GetFiles(@$"/home/{Environment.UserName}/Music/", "*.*", SearchOption.AllDirectories))
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
                continue;
            }

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
}