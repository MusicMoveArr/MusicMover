using CliFx;
using MusicMover.Jobs;
using Quartz;
using Quartz.Impl;

class Program
{
    public static string[] ConsoleArguments { get; private set; }
    public static async Task Main(string[] args)
    {
        ATL.Settings.OutputStacktracesToConsole = false;
        {
        }
        
        ConsoleArguments = args;
        
        string? cronExpression = Environment.GetEnvironmentVariable("CRON");
        if (!string.IsNullOrWhiteSpace(cronExpression))
        {
            await CreateSchedulerAsync(cronExpression);
            while (true)
            {
                Thread.Sleep(1000);
            }
        }

        await new CliApplicationBuilder()
            .AddCommandsFromThisAssembly()
            .Build()
            .RunAsync(args);
    }
    static async Task CreateSchedulerAsync(string cronExpression)
    {
        var factory = new StdSchedulerFactory();
        var scheduler = await factory.GetScheduler();
        await scheduler.Start();

        var job = JobBuilder.Create<CronJob>()
            .WithIdentity("cronJob", "group")
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity("trigger", "group")
            .WithCronSchedule(cronExpression)
            .Build();

        await scheduler.ScheduleJob(job, trigger);
    }
}