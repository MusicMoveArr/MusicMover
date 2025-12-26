using System.Diagnostics;
using MusicMover.Helpers;
using Spectre.Console;

namespace MusicMover.Rules.Machine;

public class SimpleRuleEngine
{
    public List<Rule> Rules { get; }

    public SimpleRuleEngine()
    {
        Rules = new List<Rule>();
    }
    
    public void AddRule<T>() where T : Rule
    {
        Rules.Add(Activator.CreateInstance<T>());
    }

    public async Task<List<StateResult>> RunAsync(StateObject state)
    {
        List<StateResult> results = new List<StateResult>();
        foreach (var rule in Rules)
        {
            rule.StateObject = state;
            if (!rule.Required)
            {
                continue;
            }
            
            Stopwatch sw = Stopwatch.StartNew();
            var result = await rule.ExecuteAsync();
            sw.Stop();
            result.TaskName =  rule.GetType().Name;
            
            results.Add(result);

            if (!result.Success && rule.ContinueType == ContinueType.Stop)
            {
                break;
            }
        }

        AnsiConsole.WriteLine(Markup.Escape($"File: '{state.MediaHandler.FileInfo.FullName}'"));
        foreach (var result in results)
        {
            Logger.WriteLine($"Task: {result.TaskName}, Success: {result.Success}, {result.Message}", true);
            foreach (var log in result.AdditionalLogs)
            {
                Logger.WriteLine($"   => '{log}'", true);
            }
        }

        return results;
    }
}