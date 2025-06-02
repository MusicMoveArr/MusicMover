using System.Diagnostics;
using MusicMover.Models;
using Newtonsoft.Json;

namespace MusicMover.Services;

public class FingerPrintService
{
    public async Task<FpcalcOutput?> GetFingerprintAsync(string filePath)
    {
        // Start a new process for fpcalc
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "fpcalc",  // Command to call
                Arguments = $"-json \"{filePath}\"",  // Path to the audio file
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        // Start the process and read the output
        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        // Check if fingerprint was successfully generated
        if (process.ExitCode != 0)
        {
            return null;
        }

        return ParseFingerprint(output);
    }

    private FpcalcOutput? ParseFingerprint(string output)
    {
        // Deserialize JSON output to FpcalcOutput object
        var result = JsonConvert.DeserializeObject<FpcalcOutput>(output);
        
        if (result == null || string.IsNullOrEmpty(result.Fingerprint))
        {
            return null;
        }
        
        return result;
    }
}