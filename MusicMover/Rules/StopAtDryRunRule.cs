using MusicMover.Rules.Machine;

namespace MusicMover.Rules;

public class StopAtDryRunRule : Rule
{
    public override bool Required => StateObject.Options.IsDryRun;
    public override ContinueType ContinueType { get; } =  ContinueType.Stop;
    public override async Task<StateResult> ExecuteAsync()
    {
        return new StateResult(false, "Dry run is enabled");
    }
}