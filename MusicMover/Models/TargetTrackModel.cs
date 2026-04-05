namespace MusicMover.Models;

public class TargetTrackModel
{
    public string Artist { get; set; }
    public string Album { get; set; }
    public string Title { get; set; }

    public TargetTrackModel(string artist, string album, string title)
    {
        Artist = artist;
        Album = album;
        Title = title;
    }
}