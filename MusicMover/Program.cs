using CliFx;

class Program
{
    public static async Task Main(string[] args)
    {
        ATL.Settings.OutputStacktracesToConsole = false;

        await new CliApplicationBuilder()
            .AddCommandsFromThisAssembly()
            .Build()
            .RunAsync(args);
    }
}