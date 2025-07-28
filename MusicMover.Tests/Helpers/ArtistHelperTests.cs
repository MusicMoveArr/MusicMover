using MusicMover.Helpers;
using Shouldly;

namespace MusicMover.Tests.Helpers;

public class ArtistHelperTests
{
    [Theory]
    [InlineData("Some,Artist", "Some")]
    [InlineData("Some & Artist", "Some")]
    [InlineData("Some feat Artist", "Some")]
    public void GetUncoupledArtistName_Valid_Test(string artist, string expected)
    {
        string artistName = ArtistHelper.GetUncoupledArtistName(artist);
        artistName.ShouldBe(expected);
    }
}