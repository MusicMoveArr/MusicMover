using System.Diagnostics;

namespace MusicMover;

public class CorruptionFixer
{
    private const string FileExtensionPostfix = "___fixed";
    private const int FfMpegSuccessCode = 0;
    
    public bool FixCorruption(FileInfo input)
    {
        string tempFile = $"{input.FullName}{FileExtensionPostfix}{input.Extension}";
        
        ProcessStartInfo ffmpegStartInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i \"{input.FullName}\" -c copy -movflags +faststart \"{tempFile}\"",
            RedirectStandardOutput = true,  // Redirect standard output
            RedirectStandardError = true,   // Redirect standard error
            UseShellExecute = false,        // Necessary to redirect output
            CreateNoWindow = true           // Prevents the creation of a console window
        };
        Process ffmpegProcess = Process.Start(ffmpegStartInfo);
        
        ffmpegProcess.WaitForExit();
        
        if (ffmpegProcess.ExitCode != FfMpegSuccessCode)
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
            return false;
        }

        File.Move(tempFile, input.FullName, true);
        
        return true;
    }
}