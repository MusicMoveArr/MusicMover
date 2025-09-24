using McMaster.NETCore.Plugins;
using MusicMoverPlugin;

namespace MusicMover.Helpers;

public class PluginHelper
{
    public static List<IPlugin> LoadPlugins()
    {
        List<IPlugin> plugins = new List<IPlugin>();
        var pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");

        if (!Directory.Exists(pluginsDir))
        {
            return plugins;
        }
        
        List<PluginLoader> loaders = new List<PluginLoader>();
        
        foreach (var dir in Directory.GetDirectories(pluginsDir))
        {
            var dirName = Path.GetFileName(dir);
            var pluginDll = Path.Combine(dir, dirName + ".dll");
            if (File.Exists(pluginDll))
            {
                var loader = PluginLoader.CreateFromAssemblyFile(
                    pluginDll,
                    sharedTypes: new [] { typeof(IPlugin) });
                loaders.Add(loader);
            }
        }

        // Create an instance of plugin types
        foreach (var loader in loaders)
        {
            foreach (var pluginType in loader
                         .LoadDefaultAssembly()
                         .GetTypes()
                         .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract))
            {
                // This assumes the implementation of IPlugin has a parameterless constructor
                IPlugin plugin = (IPlugin)Activator.CreateInstance(pluginType);
                plugins.Add(plugin);
            }
        }

        return plugins;
    }
}