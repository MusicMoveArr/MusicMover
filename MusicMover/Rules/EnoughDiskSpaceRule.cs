using MusicMover.Rules.Machine;

namespace MusicMover.Rules;

public class EnoughDiskSpaceRule : Rule
{
    private const long MinAvailableDiskSpace = 5000; //GB
    public override bool Required { get; } = true;
    public override ContinueType ContinueType { get; } = ContinueType.Stop;
    
    public override async Task<StateResult> ExecuteAsync()
    {
        if (!EnoughDiskSpace(StateObject.Options.ToDirectory))
        {
            return new StateResult(false, "Not enough diskspace left! <5GB on target directory>");
        }

        return new StateResult(true);
    }
    
    private bool EnoughDiskSpace(string toDirectory)
    {
        DriveInfo drive = new DriveInfo(toDirectory);

        if (!drive.IsReady)
        {
            return false;
        }

        return drive.AvailableFreeSpace > MinAvailableDiskSpace * (1024 * 1024);
    }
}