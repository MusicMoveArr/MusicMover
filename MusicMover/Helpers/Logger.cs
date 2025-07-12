using Spectre.Console;

namespace MusicMover.Helpers;

public static class Logger
{
    public static void WriteLine(string message, bool isDebug = false)
    {
        if (!isDebug || (isDebug && CliCommands.Debug))
        {
            AnsiConsole.WriteLine(message);
        }
    }
}