using ATL;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using MusicMover.Helpers;
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
        MiniMediaMetadataService miniMediaMetadataService = new MiniMediaMetadataService(baseUrl, providerTypes);
        string path = @$"/home/{Environment.UserName}/Music/";
        
        foreach (string filePath in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
        {
            Track track = new Track(filePath);
            MediaFileInfo mediaFileInfo = new MediaFileInfo(track);

            string uncoupledArtistName = ArtistHelper.GetUncoupledArtistName(track.Artist);
            string uncoupledAlbumArtistName = ArtistHelper.GetUncoupledArtistName(track.AlbumArtist);

            var matches = await miniMediaMetadataService.GetMatchesAsync(
                mediaFileInfo, 
                uncoupledArtistName,
                uncoupledAlbumArtistName, 
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
}