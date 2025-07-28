using System.Text.RegularExpressions;
using FuzzySharp;

namespace MusicMover.Helpers;

public class FuzzyHelper
{
    public static bool ExactNumberMatch(string? value1, string? value2)
    {
        if (string.IsNullOrWhiteSpace(value1) || 
            string.IsNullOrWhiteSpace(value2))
        {
            return false;
        }
        
        string regexPattern = "[0-9]*";
        var value1Match = Regex.Matches(value1, regexPattern)
            .Where(match => !string.IsNullOrWhiteSpace(match.Value))
            .Select(match => int.Parse(match.Value))
            .ToList();
        
        var value2Match = Regex.Matches(value2, regexPattern)
            .Where(match => !string.IsNullOrWhiteSpace(match.Value))
            .Select(match => int.Parse(match.Value))
            .ToList();

        if (value1Match.Count != value2Match.Count)
        {
            return false;
        }

        for (int i = 0; i < value1Match.Count; i++)
        {
            if (value1Match[i] != value2Match[i])
            {
                return false;
            }
        }

        return true;
    }

    public static int FuzzTokenSortRatioToLower(string? value1, string? value2)
    {
        if (string.IsNullOrWhiteSpace(value1) || string.IsNullOrWhiteSpace(value2))
        {
            return 0;
        }

        return Fuzz.TokenSortRatio(value1.ToLower(), value2.ToLower());
    }

    public static int FuzzRatioToLower(string? value1, string? value2)
    {
        if (string.IsNullOrWhiteSpace(value1) || string.IsNullOrWhiteSpace(value2))
        {
            return 0;
        }

        return Fuzz.Ratio(value1.ToLower(), value2.ToLower());
    }
    
    public static int PartialTokenSortRatioToLower(string? value1, string? value2)
    {
        if (string.IsNullOrWhiteSpace(value1) || string.IsNullOrWhiteSpace(value2))
        {
            return 0;
        }

        return Fuzz.PartialTokenSortRatio(value1.ToLower(), value2.ToLower());
    }
    
    public static int PartialRatioToLower(string? value1, string? value2)
    {
        if (string.IsNullOrWhiteSpace(value1) || string.IsNullOrWhiteSpace(value2))
        {
            return 0;
        }

        return Fuzz.PartialRatio(value1.ToLower(), value2.ToLower());
    }
}