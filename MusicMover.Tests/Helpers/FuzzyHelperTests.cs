using MusicMover.Helpers;
using Shouldly;

namespace MusicMover.Tests.Helpers;

public class FuzzyHelperTests
{
    [Theory]
    [InlineData("1 2", "1 2")]
    [InlineData("1. 2", "1 2")]
    [InlineData("1. 2", "1, 2")]
    [InlineData("1 with some text 2", "text order 1 shouldn't 2 matter")]
    [InlineData("Title1970 3", "1970 3")]
    [InlineData("Title1970 3 1555", "1970 3, 1555")]
    public void ExactNumberMatch_Valid_Test(string value1, string value2)
    {
        FuzzyHelper.ExactNumberMatch(value1, value2).ShouldBeTrue();
    }
    
    [Theory]
    [InlineData("1 3", "1 2")]
    [InlineData("Title1970 3", "1 2")]
    [InlineData("Title1970 3", "1970 2")]
    [InlineData("1 3", "1 2")]
    public void ExactNumberMatch_Invalid_Test(string value1, string value2)
    {
        FuzzyHelper.ExactNumberMatch(value1, value2).ShouldBeFalse();
    }
    
    
    [Theory]
    [InlineData("Uppercase Does not Matter", "uppercase does not matter")]
    [InlineData("Does Uppercase not Matter", "uppercase does not matter")]
    public void FuzzTokenSortRatioToLower_Valid_100Match(string value1, string value2)
    {
        FuzzyHelper.FuzzTokenSortRatioToLower(value1, value2).ShouldBe(100);
    }
    
    [Fact]
    public void FuzzRatioToLower_Valid_100Match()
    {
        FuzzyHelper.FuzzRatioToLower("Uppercase Does not Matter", "uppercase does not matter").ShouldBe(100);
    }
    
    [Fact]
    public void FuzzRatioToLower_Valid_80Match()
    {
        FuzzyHelper.FuzzRatioToLower("Does Uppercase not Matter", "uppercase does not matter").ShouldBe(80);
    }
    
    [Theory]
    [InlineData("Does Uppercase not Matter", "uppercase does not matter")]
    [InlineData("Uppercase Does not Matter", "uppercase does not matter")]
    [InlineData("Does not Matter", "uppercase does not matter")]
    public void PartialTokenSortRatioToLower_Valid_100Match(string value1, string value2)
    {
        FuzzyHelper.PartialTokenSortRatioToLower(value1, value2).ShouldBe(100);
    }
}