using System.Text;

namespace MusicMover.Rules.Machine;

public class StateResult
{
    public string TaskName { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; }
    public List<string> AdditionalLogs { get; } = new List<string>();

    public StateResult(bool success, string message)
    {
        this.Success = success;
        this.Message = message;
    }
    
    public StateResult(bool success)
    {
        this.Success = success;
        this.Message = string.Empty;
    }

    public StateResult()
        : this(false)
    {
        
    }

    public void LogWarning(string message)
    {
        AdditionalLogs.Add($"[{DateTime.Now.ToString("HH:mm:ss")}][Warn] '{message}'");
    }

    public void LogInfo(string message)
    {
        AdditionalLogs.Add($"[{DateTime.Now.ToString("HH:mm:ss")}][Info] '{message}'");
    }

    public void LogError(string message)
    {
        AdditionalLogs.Add($"[{DateTime.Now.ToString("HH:mm:ss")}][Error] '{message}'");
    }
}