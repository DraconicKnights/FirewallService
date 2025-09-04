using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using DragonUtilities.Enums;
using FirewallAPI.API;
using FirewallAPI.Attributes;
using FirewallCore.Core;
using FirewallEvent.Events.Core;
using FirewallInterface.Interface;

namespace FirewallCore.Utils;

internal class PluginManger
{
    private readonly List<IPlugin> _plugins = new();
    public IReadOnlyList<IPlugin> Plugins => _plugins;
    private readonly Dictionary<IPlugin, Guid> _pluginIdentifier = new ();
    
    public void LoadPlugins(string pluginDirPath)
    {
        var logger = FirewallServiceProvider.Instance.GetLogger;
        
        var loaderLines = new[]
        {
            "🔌 FirewallService Plugin Loader 🔌",
            "",
            $"Plugins Directory : {pluginDirPath}",
            $"Scan Time         : {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
        };
        
        FirewallServiceProvider.Instance.LogRaw(MessageUtiliity.BuildAsciiBox(loaderLines));

        
        AssemblyLoadContext.Default.Resolving += (context, asmName) =>
        {
            var candidate = Path.Combine(pluginDirPath, asmName.Name + ".dll");
            if (File.Exists(candidate))
            {
                return context.LoadFromAssemblyPath(candidate);
            }
            return null;
        };
        
        if (!Directory.Exists(pluginDirPath))
        {
            logger.Log(
                "Plugin directory '" + pluginDirPath + "' not found; creating it.",
                LogLevel.WARNING);
            Directory.CreateDirectory(pluginDirPath);
        }
        else
        {
            logger.Log(
                "Loading plugins from '" + pluginDirPath + "'.",
                LogLevel.DEBUG);
        }

        var dlls = Directory.GetFiles(pluginDirPath, "*.dll");
        if (dlls.Length == 0)
        {
            logger.Log($"No plugins found in '{pluginDirPath}'.", LogLevel.INFO);
            return;
        }

        var discovered = new List<PluginBase>();
        foreach (var file in dlls)
        {
            try
            {
                var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
                foreach (var type in asm.GetTypes())
                {
                    if (!typeof(PluginBase).IsAssignableFrom(type) || type.IsAbstract)
                        continue;

                    var inst = (PluginBase) Activator.CreateInstance(type)!;
                    discovered.Add(inst);
                }
            }
            catch (Exception ex)
            {
                logger.Log(
                    "Error loading assembly '" + file + "': " + ex.Message,
                    LogLevel.ERROR);
            }
        }

        var nameLookup = discovered
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var pb in discovered)
        {
            var attr = pb.GetType()
                         .GetCustomAttribute<PluginDependAttribute>();

            if (attr != null)
            {
                var missingReq = attr.RequiredDependencies
                                     .Where(r => !nameLookup.ContainsKey(r))
                                     .ToArray();
                if (missingReq.Length > 0)
                {
                    logger.Log(
                        "Skipping '" + pb.Name
                        + "'; missing required deps: "
                        + string.Join(", ", missingReq),
                        LogLevel.ERROR);
                    continue;
                }

                var missingOpt = attr.OptionalDependencies
                                     .Where(o => !nameLookup.ContainsKey(o))
                                     .ToArray();
                if (missingOpt.Length > 0)
                {
                    logger.Log(
                        "Plugin '" + pb.Name
                        + "': optional deps not found: "
                        + string.Join(", ", missingOpt),
                        LogLevel.WARNING);
                }
            }

            pb.Initialize(
                logger,
                FirewallEventService.Instance,
                new PluginConfigManager(FirewallServiceProvider.Instance._crypto, FileFormat.Yaml),
                new PluginHandler(),
                new FirewallApiController(FirewallServiceProvider.Instance, FirewallServiceProvider.Instance.CommandManager),
                new PluginScopedSchedulerService(FirewallServiceProvider.Instance.SchedulerService)
            );

            _pluginIdentifier.Add(pb, Guid.NewGuid());
            _plugins.Add(pb);
            
            // Use Scheduler to run this later and activate the plugin
            FirewallServiceProvider.Instance.SchedulerService.ScheduleOnce(TimeSpan.FromSeconds(10), () =>
            {
                try
                {
                    pb.OnEnable();
                    RegisterPluginCommands(pb);

                    logger.Log(
                        "Loaded plugin: " 
                        + pb.Name 
                        + " v" 
                        + pb.Version 
                        + " by " 
                        + pb.Author,
                        LogLevel.DEBUG);
                    logger.Log(
                        "  Description: " 
                        + pb.Description,
                        LogLevel.DEBUG);
                }
                catch (Exception ex)
                {
                    logger.Log(
                        "Error enabling '" 
                        + pb.Name 
                        + "': " 
                        + ex.Message,
                        LogLevel.ERROR);
                }
            });
        }
        
        var now = DateTime.UtcNow;
        var loadLines = new List<string>
        {
            "✅ Plugin Load Summary",
            "",
            "Time (UTC)           : " + now.ToString("yyyy-MM-dd HH:mm:ss"),
            "Assemblies Scanned   : " + dlls.Length,
            "Plugins Discovered   : " + _plugins.Count,
            "Total Commands       : " + _plugins.Sum(p => p.GetCommands().Count())
        };
        
        loadLines.AddRange(_plugins
            .Select(p => $"{p.Name} v{p.Version} by {p.Author}")
        );

        FirewallServiceProvider.Instance.LogRaw(MessageUtiliity.BuildAsciiBox(loadLines));
    }
    
    public void UnloadPlugins()
    {
        if (_plugins.Count == 0)
        {
            var emptyLines = new[]
            {
                "❌ Plugin Unload Summary",
                "",
                "No plugins to unload."
            };
            FirewallServiceProvider.Instance.LogRaw(MessageUtiliity.BuildAsciiBox(emptyLines));
            return;
        }
        
        var now = DateTime.UtcNow;
        var unloadLines = new List<string>
        {
            "❌ Plugin Unload Summary",
            "",
            "Time (UTC)           : " + now.ToString("yyyy-MM-dd HH:mm:ss"),
            "Plugins Unloaded     : " + _plugins.Count
        };
        
        unloadLines.AddRange(_plugins
            .Select(p => $"{p.Name} v{p.Version} by {p.Author}")
        );

        FirewallServiceProvider.Instance.LogRaw(MessageUtiliity.BuildAsciiBox(unloadLines));
        
        // Now perform the actual unload steps
        foreach (var plugin in _plugins)
        {
            UnregisterPluginCommands(plugin);
            plugin.OnDisable();
            _pluginIdentifier.Remove(plugin);
        }

        _plugins.Clear();
        _pluginIdentifier.Clear();
    }

    private void RegisterPluginCommands(IPlugin plugin)
    {
        foreach (var command in plugin.GetCommands())
        {
            FirewallServiceProvider.Instance.CommandManager.RegisterCommand(command);
        }
    }
    
    private void UnregisterPluginCommands(IPlugin plugin)
    {
        foreach (var command in plugin.GetCommands())
        {
            FirewallServiceProvider.Instance.CommandManager.UnregisterCommand(command);
        }
    }
    
    // IPluginHandler implementation
    public bool TryGetPlugin(string name, out IPlugin plugin)
    {
        plugin = _plugins.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))!;
        return plugin != null;
    }

    public bool TryGetPlugin<T>(out T plugin) where T : class, IPlugin
    {
        plugin = _plugins.OfType<T>().FirstOrDefault()!;
        return plugin != null;
    }
    public IPlugin GetPlugin(Guid identifier)
    {
        foreach (var guid in _pluginIdentifier)
        {
            if (guid.Value == identifier)
            {
                return guid.Key;
            }

            FirewallServiceProvider.Instance.GetLogger.Log($"Failed to find plugin with identifier: {identifier}", LogLevel.ERROR);
            return null!;
        }
        return null!;
    }
     
    public List<IPlugin> GetPlugins() => _plugins;
    public Dictionary<IPlugin, Guid> GetPluginIdentifier() => _pluginIdentifier;
}