namespace MusicMover.Models;

public class MusicBrainzRecordingQueryModel
{
    public string Created { get; set; }
    public int Count { get; set; }
    public int Offset { get; set; }
    public List<MusicBrainzRecordingQueryEntityModel> Recordings { get; set; }
}