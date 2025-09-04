using System.Reflection;
using DragonUtilities.Interfaces;
using FirewallAPI.Attributes;
using FirewallEvent.Events.Core;
using FirewallInterface.Interface;

namespace FirewallAPI.API;

/// <summary>
/// Plugin Base for External Plugins
/// Attach to your plugin so it may be initialized for use
/// </summary>
public abstract class PluginBase : IPlugin
{
    ///--- Core Plugin Base Utils --- ///
    
    /// <summary>
    /// Plugin Name
    /// Used for getting the name of the plugin
    /// </summary>
    public abstract string Name { get; }
    /// <summary>
    /// Plugin Description
    /// Used for getting the description of the plugin
    /// </summary>
    public abstract string Description { get; }
    /// <summary>
    /// Plugin Author
    /// Used for getting the author of the plugin
    /// </summary>
    public abstract string Author { get; }
    /// <summary>
    /// Plugin Version
    /// Used for getting the version of the plugin
    /// </summary>
    public abstract string Version { get; }
    
    /// <summary>
    /// Plugin OnEnable Call
    /// This is called once the plugin has been fully intiailzied 
    /// </summary>
    public abstract void OnEnable();
    /// <summary>
    /// Plugin OnDisable Call
    /// This is called upon the plugin being shutdown
    /// </summary>
    public abstract void OnDisable();
    public abstract IEnumerable<ICommand> GetCommands();

    /// <summary>
    /// Logger
    /// Provides logging functionality for the plugin.
    /// </summary>
    protected ILogger Logger { get; private set; } = null!;

    /// <summary>
    /// Provides access to the core event management system for handling firewall-related events.
    /// Used to subscribe, unsubscribe, and publish events within the plugin system.
    /// </summary>
    protected IFirewallEventService FirewallEventService { get; private set; } = null!;

    /// <summary>
    /// Configuration Manager
    /// Provides access to manage plugin-specific configurations, including loading, saving, and handling configuration files.
    /// </summary>
    protected IPluginConfigManager ConfigManager { get; private set; } = null!;

    /// <summary>
    /// Handler for managing plugins within the system.
    /// Provides access to loaded plugins and their interaction mechanisms.
    /// </summary>
    protected IPluginHandler PluginHandler { get; private set; } = null!;
    
    protected ISchedulerService SchedulerService { get; private set; } = null!;

    /// --- Private PluginBase Utils access point for ease of use --- ///
    
    /// <summary>
    /// Represents the core API for interacting with firewall functionalities.
    /// Provides methods for event subscriptions, command handling, and retrieving blocked IP addresses.
    /// </summary>
    private IFirewallAPI FirewallAPI { get; set; } = null!;

    internal void Initialize(ILogger logger, IFirewallEventService firewallEventService, 
        IPluginConfigManager config, IPluginHandler handler, IFirewallAPI firewallApi, ISchedulerService schedulerService)
    {
        Logger = logger;
        FirewallEventService = firewallEventService;
        ConfigManager = config;
        PluginHandler = handler;
        FirewallAPI = firewallApi;
        SchedulerService = schedulerService;
        
        AutoWireConfigs();
    }
    
    private void AutoWireConfigs()
    {
        var members = this.GetType()
            .GetMembers(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
            .Where(m => m.GetCustomAttribute<ConfigAttribute>() != null);

        foreach (var m in members)
        {
            var attr = m.GetCustomAttribute<ConfigAttribute>()!;
            Type   t;
            Action<object?> setter;

            if (m is PropertyInfo pi && pi.CanWrite)
            {
                t      = pi.PropertyType;
                setter = v => pi.SetValue(this, v);
            }
            else if (m is FieldInfo fi)
            {
                t      = fi.FieldType;
                setter = v => fi.SetValue(this, v);
            }
            else
                continue;

            // build the generic call ConfigManager.LoadConfig<T>(Name, fileName)
            var fileName = string.IsNullOrWhiteSpace(attr.FileName)
                ? t.Name
                : attr.FileName!;
            var loadMethod = typeof(IPluginConfigManager)
                .GetMethod(nameof(IPluginConfigManager.LoadConfig))!
                .MakeGenericMethod(t);

            var cfg = loadMethod.Invoke(ConfigManager, new object[]{ Name, fileName })!;
            setter(cfg);
        }
    }
    
    /// --- Core PluginBase Util Function Calls --- ///

    /// <summary>
    /// Loads a configuration file for the current plugin.
    /// The file is located under Plugins/{Name}/Config.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object to be loaded. Must have a parameterless constructor.</typeparam>
    /// <param name="fileName">The name of the configuration file to be loaded.</param>
    /// <returns>An instance of the configuration object loaded from the specified file.</returns>
    protected T LoadConfig<T>(string fileName) where T : new()
        => ConfigManager!.LoadConfig<T>(Name, fileName);

    /// <summary>
    /// Saves the specified configuration object to the file under Plugins/{Name}/Config.
    /// </summary>
    /// <param name="fileName">The name of the configuration file to save.</param>
    /// <param name="cfg">The configuration object to be saved.</param>
    /// <typeparam name="T">The type of the configuration object.</typeparam>
    protected void SaveConfig<T>(string fileName, T cfg) where T : new()
        => ConfigManager!.SaveConfig(Name, fileName, cfg);

    /// <summary>
    /// Retrieves a list of all IP addresses currently blocked by the firewall.
    /// </summary>
    /// <returns>A read-only list of strings representing the blocked IP addresses.</returns>
    protected IReadOnlyList<string> GetBlockedIPs() => FirewallAPI.GetBlockedIPs();

    /// <summary>
    /// Retrieves the list of IP addresses whitelisted by the firewall.
    /// </summary>
    /// <returns>A read-only list of strings representing the whitelisted IP addresses.</returns>
    protected IReadOnlyList<string> GetWhitelistedIPs() => FirewallAPI.GetWhitelistedIPs();

    /// <summary>
    /// Retrieves an instance of the specified command type.
    /// Sets the output parameter to the resulting command instance or null if the command cannot be retrieved.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command to retrieve. Must implement <see cref="ICommand"/>.</typeparam>
    /// <param name="command">The output parameter that will hold the retrieved command instance.</param>
    protected void GetCommand<TCommand>(out ICommand command) where TCommand : ICommand
    {
        command = null!;
        FirewallAPI.GetCommand<TCommand>(out var command1);
    }

    /// <summary>
    /// Processes a given command and performs the associated operations.
    /// </summary>
    /// <param name="command">The command to be processed.</param>
    /// <param name="parameters">Optional parameters associated with the command.</param>
    /// <returns>Returns a boolean value indicating whether the command was processed successfully.</returns>
    protected void ProcessCommand<TCommand>(string args) where TCommand : ICommand
    {
        FirewallAPI.ProcessCommand<TCommand>(args);
    }
}