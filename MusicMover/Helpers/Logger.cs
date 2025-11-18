using Spectre.Console;

namespace MusicMover.Helpers;

public static class Logger
{
    public static void WriteLine(string message, bool isDebug = false)
    {
        if (!isDebug || (isDebug && MoveCommands.Debug))
        {
            if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
            {
                Console.WriteLine(message);
            }
            else
            {
                AnsiConsole.WriteLine(message);
            }
        }
    }
}