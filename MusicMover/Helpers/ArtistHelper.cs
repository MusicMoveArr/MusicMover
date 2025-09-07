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
}