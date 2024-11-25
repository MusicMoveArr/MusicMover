using System.Diagnostics;

namespace MusicMover;

public class CorruptionFixer
{
    private const string FileExtensionPostfix = "___fixed";
    private const int FfMpegSuccessCode = 0;
    
    public bool FixCorruption(FileInfo input)
    {
        string tempFile = $"{input.FullName}{FileExtensionPostfix}{input.Extension}";
        bool corruptedBefore = CanReadTags(input.FullName) == false;
        
        ProcessStartInfo ffmpegStartInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i \"{input.FullName}\" -c copy -movflags faststart \"{tempFile}\"",
            RedirectStandardOutput = true,  // Redirect standard output
            RedirectStandardError = true,   // Redirect standard error
            UseShellExecute = false,        // Necessary to redirect output
            CreateNoWindow = true           // Prevents the creation of a console window
        };
        Process ffmpegProcess = Process.Start(ffmpegStartInfo);
        
        //Process ffmpegProcess = Process.Start("ffmpeg", $"-i \"{input.FullName}\" -c copy -movflags faststart \"{tempFile}\"");
        
        ffmpegProcess.WaitForExit();
        
        if (ffmpegProcess.ExitCode != FfMpegSuccessCode)
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
            return false;
        }

        bool corruptedAfter = CanReadTags(tempFile) == false;

        if (corruptedBefore && !corruptedAfter)
        {
            File.Move(tempFile, input.FullName, true);
            return true;
        }
        
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
        
        return false;
    }
    
    private bool CanReadTags(string inputFile)
    {
        try
        {
            TagLib.File.Create(inputFile);
            return true;
        }
        catch { }
        return false;
    }
}