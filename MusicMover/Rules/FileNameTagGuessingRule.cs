using System.Text.RegularExpressions;
using MusicMover.Helpers;
using MusicMover.Rules.Machine;

namespace MusicMover.Rules;

public class FileNameTagGuessingRule : Rule
{
    public override bool Required => StateObject.Options.FileNameTagGuessing;
    public override ContinueType ContinueType { get; } =  ContinueType.Continue;
    
    private static readonly string[] patterns =
    {
        //Artist - Album - Track(or Track-Cd) - Track.ext
        @"^(?<artist>.+?)\s-\s(?<album>.+?)\s-\s\d{2}(?:-\d{2})?\s-\s(?<track>.+?)\.(mp3|flac|m4a|opus|wav|aiff)$",
        
        //Artist - Album - Track(optional) Track.ext
        @"^(?<artist>.+?)\s-\s(?<album>.+?)\s-\s\d+\s(?<track>.+?)\.(mp3|flac|m4a|opus|wav|aiff)$",
        
        //Artist - Album - CD-Track Track.ext
        @"^(?<artist>.+?)\s-\s(?<album>.+?)\s-\s\d{2}-\d{2}\s(?<track>.+?)\.(mp3|flac|m4a|opus|wav|aiff)$",

        //TrackNumber. Artist - Track.ext
        @"^\d{1,3}\.\s(?<artist>.+?)\s-\s(?<track>.+?)\.(mp3|flac|m4a|opus|wav|aiff)$",

        //Artist - Track.ext
        @"^(?<artist>.+?)\s-\s(?<track>.+?)\.(mp3|flac|m4a|opus|wav|aiff)$",

        //Artist_Album_TrackNum_Track.ext  (underscores)
        @"^(?<artist>[^_]+)_(?<album>[^_]+)_\d{1,3}_(?<track>[^_]+)\.(mp3|flac|m4a|opus|wav|aiff)$",

        //[Year] Artist - Album - Track.ext
        @"^\[\d{4}\]\s(?<artist>.+?)\s-\s(?<album>.+?)\s-\s(?<track>.+?)\.(mp3|flac|m4a|opus|wav|aiff)$",

        //TrackNum - Track.ext  (album-folder assumed, no artist/album in filename)
        @"^\d{1,3}\s-\s(?<track>.+?)\.(mp3|flac|m4a|opus|wav|aiff)$",

        //Artist - Track (Year).ext
        @"^(?<artist>.+?)\s-\s(?<track>.+?)\s\(\d{4}\)\.(mp3|flac|m4a|opus|wav|aiff)$",

        //CD#-Track# Artist - Album - Track.ext  (multi-disc)
        @"^\d{1,2}-\d{2}\s(?<artist>.+?)\s-\s(?<album>.+?)\s-\s(?<track>.+?)\.(mp3|flac|m4a|opus|wav|aiff)$",

        //Artist - Album (Year) - TrackNum. Track.ext
        @"^(?<artist>.+?)\s-\s(?<album>.+?)\s\(\d{4}\)\s-\s\d{1,3}\.\s(?<track>.+?)\.(mp3|flac|m4a|opus|wav|aiff)$",

        //YYYYMMDD_Artist_Track.ext
        @"^\d{8}_(?<artist>[^_]+)_(?<track>.+?)\.(mp3|flac|m4a|opus|wav|aiff)$",

        //Artist - [Label - Cat#] - Album - TrackNum - Track.ext  (DJ/archival)
        @"^(?<artist>.+?)\s-\s\[.+?\]\s-\s(?<album>.+?)\s-\s\d{1,3}\s-\s(?<track>.+?)\.(mp3|flac|m4a|opus|wav|aiff)$",
    };
    
    private static readonly Regex[] compiled = patterns
        .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase))
        .ToArray();
    
    public override async Task<StateResult> ExecuteAsync()
    {
        SetTagsFromFileName();
        return new StateResult(true);
    }

    private void SetTagsFromFileName()
    {
        StateObject.MediaHandler.SetMediaTagValue(StateObject.MediaHandler.Artist.Replace("，", ";"), "artist");
        
        string originalArtist = StateObject.MediaHandler.Artist;
        string originalAlbum = StateObject.MediaHandler.Album;
        string originalTitle = StateObject.MediaHandler.Title;
        
        StateObject.MediaHandler.SetMediaTagValue(string.Empty, "artist");
        StateObject.MediaHandler.SetMediaTagValue(string.Empty, "album");
        StateObject.MediaHandler.SetMediaTagValue(string.Empty, "title");

        string filename = StateObject.MediaHandler.FileInfo.Name;
        filename = Regex.Replace(filename, @"\s\[\d{8,11}\](?=\.\w+$)", ""); //remove at the end [111111111]
        filename = Regex.Replace(filename, @"\s\(\d{1,3}\)(?=\.\w+$)", ""); //remove at the end (1)

        string discPattern = @"(\([0-9]*\))\.(mp3|flac|m4a|opus|wav|aiff)$";
        if (Regex.IsMatch(filename, discPattern))
        {
            var match = Regex.Match(filename, discPattern);
            filename = Regex.Replace(filename, discPattern, string.Empty);
            filename += "." + match.Groups[2].Value;
        }
        
        bool success = false;
        foreach (var pattern in compiled)
        {
            var m = pattern.Match(filename);
            if (!m.Success)
            {
                continue;
            }
            string artist = m.Groups["artist"].Success ? m.Groups["artist"].Value : string.Empty;
            string album  = m.Groups["album"].Success  ? m.Groups["album"].Value  : string.Empty;
            string track  = m.Groups["track"].Value;

            if (!string.IsNullOrWhiteSpace(artist))
            {
                string singleArtist = ArtistHelper.GetUncoupledArtistName(artist);
                if (!StateObject.MediaHandler.AllArtistNames.Contains(artist))
                {
                    StateObject.MediaHandler.AllArtistNames.Add(artist);
                }
                if (!StateObject.MediaHandler.AllArtistNames.Contains(singleArtist))
                {
                    StateObject.MediaHandler.AllArtistNames.Add(singleArtist);
                }

                foreach (var a in artist.Split([','], StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!StateObject.MediaHandler.AllArtistNames.Contains(a))
                    {
                        StateObject.MediaHandler.AllArtistNames.Add(a);
                    }
                }
                
                StateObject.MediaHandler.SetMediaTagValue(artist, "artist");
            }
            if (!string.IsNullOrWhiteSpace(album))
            {
                StateObject.MediaHandler.SetMediaTagValue(album, "album");
            }
            if (!string.IsNullOrWhiteSpace(track))
            {
                StateObject.MediaHandler.SetMediaTagValue(track, "title");
            }

            success = true;
            break;
        }

        if (!success)
        {
            StateObject.MediaHandler.SetMediaTagValue(originalArtist, "artist");
            StateObject.MediaHandler.SetMediaTagValue(originalAlbum, "album");
            StateObject.MediaHandler.SetMediaTagValue(originalTitle, "title");
        }
        StateObject.MediaHandler.RefreshAllArtistNames();
    }
}