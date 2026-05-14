using MusicMover.MediaHandlers;
using Shouldly;

namespace MusicMover.Tests.Services;

public class MediaHandlerFFmpegTests
{
    /// <summary>
    /// Tests single tagname per file if they're silently dropped if not known by ffmpeg
    /// </summary>
    [Theory]
    [InlineData("", "Track01.wav", "artist")]
    [InlineData("", "output.mp3", "artist")]
    [InlineData("", "output.aiff", "title")] //found these 4 in ffmpeg's source libavformat/aiffenc.c
    [InlineData("", "output.aiff", "author")]
    [InlineData("", "output.aiff", "copyright")]
    [InlineData("", "output.aiff", "comment")]
    [InlineData("", "output.flac", "artist")]
    [InlineData("", "output.m4a", "artist")]
    [InlineData("", "output.opus", "artist")]
    //custom tags
    [InlineData("", "Track01.wav", "artist_custom")] //expected to fail
    [InlineData("", "output.mp3", "title_custom")]
    [InlineData("", "output.aiff", "title_custom")] //expected to fail
    [InlineData("", "output.flac", "artist_custom")]
    [InlineData("", "output.m4a", "artist_custom")]
    [InlineData("", "output.opus", "artist_custom")]
    //custom tags
    [InlineData("", "Track01.wav", "Tidal Track Id")] //expected to fail
    [InlineData("", "output.mp3", "Tidal Track Id")]
    [InlineData("", "output.aiff", "Tidal Track Id")] //expected to fail
    [InlineData("", "output.flac", "Tidal Track Id")]
    [InlineData("", "output.m4a", "Tidal Track Id")]
    [InlineData("", "output.opus", "Tidal Track Id")]
    public void TestSilentlyDroppedMetadataTag(string basePath, string path, string tagName)
    {
        FileInfo fileInfo = new FileInfo(Path.Join(basePath, path));
        fileInfo.Directory.CreateSubdirectory("temp");
        
        FileInfo targetFileInfo = new FileInfo(Path.Join(fileInfo.Directory.FullName, "temp", $"{fileInfo.Name}"));

        if (targetFileInfo.Exists)
        {
            targetFileInfo.Delete();
        }

        MediaHandlerFFmpeg inputFile = new MediaHandlerFFmpeg(fileInfo);
        inputFile.SetMediaTagValue(Guid.NewGuid().ToString()[..10], tagName);
        inputFile.SaveTo(targetFileInfo);
        
        MediaHandlerFFmpeg outputFile = new MediaHandlerFFmpeg(targetFileInfo);
        bool hasTagValue = !string.IsNullOrWhiteSpace(outputFile.GetMediaTagValue(tagName));
        hasTagValue.ShouldBeTrue($"Tag should be written but is silently dropped '{tagName}'\r\n{fileInfo.FullName}");
    }
    
    /// <summary>
    /// Tests multiple tagnames at once, testing known and unknown at the sametime to see if they're silently dropped
    /// </summary>
    [Theory]
    //custom tags
    [InlineData("", "Track01.wav", "artist", "Tidal Track Id", "artist_custom")] //expected to fail
    [InlineData("", "output.mp3", "artist", "Tidal Track Id", "title_custom")]
    [InlineData("", "output.aiff", "artist", "Tidal Track Id", "title_custom")] //expected to fail
    [InlineData("", "output.aiff", "author", "Tidal Track Id", "title_custom")] //expected to still fail
    [InlineData("", "output.flac", "artist", "Tidal Track Id", "artist_custom")]
    [InlineData("", "output.m4a", "artist", "Tidal Track Id", "artist_custom")]
    [InlineData("", "output.opus", "artist", "Tidal Track Id", "artist_custom")]
    public void TestSilentlyDroppedMixedMetadataTags(string basePath, string path, params string[] tagNames)
    {
        FileInfo fileInfo = new FileInfo(Path.Join(basePath, path));
        fileInfo.Directory.CreateSubdirectory("temp");
        
        FileInfo targetFileInfo = new FileInfo(Path.Join(fileInfo.Directory.FullName, "temp", $"{fileInfo.Name}"));

        if (targetFileInfo.Exists)
        {
            targetFileInfo.Delete();
        }
        
        MediaHandlerFFmpeg inputFile = new MediaHandlerFFmpeg(fileInfo);
        foreach (string tagName in tagNames)
        {
            inputFile.SetMediaTagValue(Guid.NewGuid().ToString()[..10], tagName);
        }
        inputFile.SaveTo(targetFileInfo);
        
        MediaHandlerFFmpeg outputFile = new MediaHandlerFFmpeg(targetFileInfo);
        foreach (string tagName in tagNames)
        {
            bool hasTagValue = !string.IsNullOrWhiteSpace(outputFile.GetMediaTagValue(tagName));
            hasTagValue.ShouldBeTrue($"Tags should be written but are silently dropped '{tagName}'\r\n{fileInfo.FullName}");
        }
    }
    
    /// <summary>
    /// Tests multiple tagnames at once, testing known and unknown at the sametime to see if they're silently dropped but any surviving tag will pass
    /// </summary>
    [Theory]
    [InlineData("", "Track01.wav", "artist", "Tidal Track Id", "artist_custom")] //expected to fail
    [InlineData("", "output.mp3", "artist", "Tidal Track Id", "title_custom")]
    [InlineData("", "output.aiff", "artist", "Tidal Track Id", "title_custom")] //expected to fail
    [InlineData("", "output.aiff", "author", "Tidal Track Id", "title_custom")] //expected to still fail
    [InlineData("", "output.flac", "artist", "Tidal Track Id", "artist_custom")]
    [InlineData("", "output.m4a", "artist", "Tidal Track Id", "artist_custom")]
    [InlineData("", "output.opus", "artist", "Tidal Track Id", "artist_custom")]
    public void TestSilentlyDroppedMixedMetadataTagsRelaxed(string basePath, string path, params string[] tagNames)
    {
        FileInfo fileInfo = new FileInfo(Path.Join(basePath, path));
        fileInfo.Directory.CreateSubdirectory("temp");
        
        FileInfo targetFileInfo = new FileInfo(Path.Join(fileInfo.Directory.FullName, "temp", $"{fileInfo.Name}"));

        if (targetFileInfo.Exists)
        {
            targetFileInfo.Delete();
        }
        
        MediaHandlerFFmpeg inputFile = new MediaHandlerFFmpeg(fileInfo);
        foreach (string tagName in tagNames)
        {
            inputFile.SetMediaTagValue(Guid.NewGuid().ToString()[..10], tagName);
        }
        inputFile.SaveTo(targetFileInfo);
        
        MediaHandlerFFmpeg outputFile = new MediaHandlerFFmpeg(targetFileInfo);
        bool anyTagExisted = false;
        foreach (string tagName in tagNames)
        {
            bool hasTagValue = !string.IsNullOrWhiteSpace(outputFile.GetMediaTagValue(tagName));
            if (hasTagValue)
            {
                anyTagExisted = true;
                break;
            }
        }
        
        anyTagExisted.ShouldBeTrue($"Tags should be written but are all silently dropped\r\n{fileInfo.FullName}");
    }
}