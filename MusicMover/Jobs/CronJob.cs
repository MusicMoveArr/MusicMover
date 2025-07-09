using CliFx;
using Quartz;
using Spectre.Console;

namespace MusicMover.Jobs;

[DisallowConcurrentExecution]
public class CronJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await new CliApplicationBuilder()
                .AddCommandsFromThisAssembly()
                .Build()
                .RunAsync(Program.ConsoleArguments);
        }
        catch (Exception e)
        {
            AnsiConsole.WriteLine(e.Message);
        }
    }
}