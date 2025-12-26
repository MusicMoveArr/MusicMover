namespace MusicMover.Rules.Machine;

public abstract class Rule
{
    public abstract bool Required { get; }
    public abstract ContinueType ContinueType { get; }
    public abstract Task<StateResult> ExecuteAsync();
    public StateObject StateObject { get; set; }
}