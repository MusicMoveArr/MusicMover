using MusicMover.MediaHandlers;
using SmartFormat;

namespace MusicMover.Helpers;

public static class ArtistHelper
{
    public static string GetUncoupledArtistName(string? artist)
    {
        if (string.IsNullOrWhiteSpace(artist))
        {
            return string.Empty;
        }
        
        string[] splitCharacters =
        [
            ",",
            "&",
            "+",
            "/",
            " feat",
            ";"
        ];

        string? newArtistName = splitCharacters
            .Where(splitChar => artist.Contains(splitChar))
            .Select(splitChar => artist.Substring(0, artist.IndexOf(splitChar)).Trim())
            .Where(split => split.Length > 0)
            .OrderBy(split => split.Length)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(newArtistName))
        {
            return artist;
        }
        return newArtistName;
    }

    public static string GetShortVersion(string? value, int length, string postfix)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.Length <= length)
        {
            return value;
        }

        return value.Substring(0, length) + postfix;
    }

    public static string GetShortWordVersion(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        string result = string.Empty;
        string[] wordSplit = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (string word in wordSplit)
        {
            if (result.Length + word.Length <= maxLength)
            {
                result += word + " ";
            }
        }

        if (string.IsNullOrEmpty(result) && value.Length >= maxLength)
        {
            result = value.Substring(0, maxLength);
        }


        char[] charsToCleanup = "`~!@#$%^&*()_+-=[]{};':\",./<>? ".ToCharArray();
        while(result.Length > 0)
        {
            if (!result.Skip(result.Length - 1).TakeLast(1).Any(c => charsToCleanup.Contains(c)))
            {
                break;
            }

            result = result.Substring(0, result.Length - 1);
        }

        return result.Trim();
    }
    
    public static string GetFormatName(MediaHandler mediaHandler, string format, string seperator)
    {
        mediaHandler.SetMediaTagValue(ReplaceDirectorySeparators(mediaHandler.Artist, seperator), "Artist");
        mediaHandler.SetMediaTagValue(ReplaceDirectorySeparators(mediaHandler.Title, seperator), "Title");
        mediaHandler.SetMediaTagValue(ReplaceDirectorySeparators(mediaHandler.Album, seperator), "Album");
        
        format = Smart.Format(format, mediaHandler);
        format = format.Trim();
        return format;
    }
    
    public static string ReplaceDirectorySeparators(string? input, string seperator)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }
        
        if (input.Contains('/'))
        {
            input = input.Replace("/", seperator);
        }
        else if (input.Contains('\\'))
        {
            input = input.Replace("\\", seperator);
        }

        return input;
    }
}