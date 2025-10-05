using MusicMover.Helpers;
using MusicMover.MediaHandlers;

namespace MusicMover.Services;

public class MediaTagWriteService
{
    public async Task UpdateArtistAsync(MediaHandler track, string artistName)
    {
        string orgValue = string.Empty;
        bool isUpdated = false;
        UpdateTrackTag(track, "artist", artistName, ref isUpdated, ref orgValue);
        UpdateTrackTag(track, "AlbumArtist", artistName, ref isUpdated, ref orgValue);
    }
    
    public void UpdateTrackTag(MediaHandler track, string tag, string value, ref bool updated, ref string? orgValue)
    {
        value = value.Trim();
        orgValue = track.GetMediaTagValue(tag);
        track.SetMediaTagValue(value, tag);
        updated = orgValue != value;
    }
    
    public async Task<bool> SafeSaveAsync(MediaHandler track, FileInfo targetFile)
    {
        string tempFile = $"{targetFile.FullName}.bak";

        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
        
        bool success = false;
        CancellationTokenSource cancellationToken = new CancellationTokenSource();
        
        var writeTask = Task.Run(() =>
        {
            try
            {
                success = track.SaveTo(new FileInfo(tempFile));
            }
            catch (Exception ex)
            {
                Logger.WriteLine(ex.Message);
            }
        });
        await Task.WhenAny(writeTask, Task.Delay(TimeSpan.FromMinutes(1)));

        if (!writeTask.IsCompleted)
        {
            cancellationToken.Cancel();
        }

        if (success && File.Exists(tempFile))
        {
            File.Move(tempFile, targetFile.FullName, true);
        }
        else if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }

        return success;
    }

    public void UpdateTag(MediaHandler track,
        string tagName, 
        string? value, 
        ref bool trackInfoUpdated)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (int.TryParse(value, out int intValue) && intValue == 0)
        {
            return;
        }
        
        string orgValue = string.Empty;
        bool tempIsUpdated = false;
        UpdateTrackTag(track, tagName, value, ref tempIsUpdated, ref orgValue);
        
        if (tempIsUpdated && !string.Equals(orgValue, value))
        {
            if (value.Length > 100)
            {
                value = value.Substring(0, 100) + "...";
            }
            if (orgValue.Length > 100)
            {
                orgValue = orgValue.Substring(0, 100) + "...";
            }
            
            Logger.WriteLine($"Updating tag '{tagName}' value '{orgValue}' =>  '{value}'", true);
            trackInfoUpdated = true;
        }
    }
    
}