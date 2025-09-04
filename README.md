# 🔥 Firewall API Service Documentation

## 🛡️ Overview

The **Firewall Service** is a robust and extensible .NET 9.0 component designed to:

- 🔍 Monitor and parse system logs in real time
- 🚫 Enforce dynamic blocking rules (e.g., via iptables)
- 📄 Manage blocklists and automate log rotation
- 💬 Expose a programmable CLI / command interface
- 🧩 Support custom plugins for additional functionality
- 🔐 Secure configuration with built-in encryption

---

## 📦 Core Features

- 🔁 **Event-Driven Architecture**  
  Powered by a singleton `FirewallEventService` for flexible, decoupled communication.

- 🚫 **Comprehensive IP Control**  
  Block, unblock and whitelist IPs on demand or via automated policies.

- ⚙️ **Modular Command System**  
  Register and execute commands through `CommandManager` for a powerful CLI experience.

- 🧩 **Plugin-Friendly Design**  
  Seamlessly add new functionality with custom plugins—no core changes required.

- 📝 **Unified Logging & Control**  
  Centralized monitoring, rule enforcement, and encrypted configuration via `FirewallService`.

---

## 🧩 Firewall API Service

### 📍 Example Plugin Usage

#### ConnectionStats Plugin

```csharp
[PluginDepend]
public class ConnectionStatsPlugin : PluginBase
{
    public override string Name => "ConnectionStats";
    public override string Description => "Tracks per-IP connection counts.";
    public override string Author => "Dragon";
    public override string Version => "1.0.0";
    
    [Config]
    public StatsConfig _config;
    
    private readonly Dictionary<string,int> _counts = new();
    
    public override void OnEnable()
    {
        Logger.Log("Plugin Started", LogLevel.INFO);
        FirewallEventService.Subscribe<ConnectionAttemptEvent>(OnConnectionAttempt);
    }

    private void OnConnectionAttempt(ConnectionAttemptEvent ev)
    {
        var ip = ev.IpAddress;
        if (!_counts.ContainsKey(ip)) _counts[ip] = 0;
        _counts[ip]++;

        if (_config.AlertThreshold > 0 && _counts[ip] >= _config.AlertThreshold)
            Logger.Log($"[Stats] {ip} reached {_counts[ip]} attempts", LogLevel.WARNING);
    }

    public override void OnDisable()
    {
        Logger.Log("Plugin Disabled", LogLevel.INFO);
    }

    public override IEnumerable<ICommand> GetCommands()
    {
        return new List<ICommand>
        {
            new StatsCommand(this),
        };
    }
    
    public int GetCount(string ip) => _counts.TryGetValue(ip, out var c) ? c : 0;
    
    private class StatsCommand : ICommand
    {
        private readonly ConnectionStatsPlugin _parent;
        public StatsCommand(ConnectionStatsPlugin parent) => _parent = parent;

        public string Name        => "stats";
        public string Description => "Shows connection count for an IP";
        public string Usage       => "stats <ip>";

        public void Execute(string[] args, IFirewallContext ctx, out string response)
        {
            if (args.Length != 1)
            {
                response = $"Usage: {Usage}";
                return;
            }
            var ip = args[0];
            var count = _parent.GetCount(ip);
            response = $"Connection attempts for {ip}: {count}";
        }

    }
}
```

---

#### ThresholdAlert Plugin

```csharp
[PluginDepend("ConnectionStats")]
public class ThresholdAlertPlugin : PluginBase
{
    private ConnectionStatsPlugin _stats;
    
    private static ThresholdAlertPlugin _instance;
    public static ThresholdAlertPlugin GetInstance() => _instance;

    public override string Name        => "ThresholdAlert";
    public override string Version     => "1.0.0";
    public override string Author      => "Bob";
    public override string Description => "Raises critical alerts on high counts.";

    public override void OnEnable()
    {
        Logger.Log("ThresholdAlertPlugin enabled.", LogLevel.INFO);
        
        _instance = this;

        if (!PluginHandler.TryGetPlugin(out _stats))
        {
            Logger.Log("ConnectionStatsPlugin not found; disabling alerts.", LogLevel.ERROR);
            return;
        }

        FirewallEventService.Subscribe<ConnectionAttemptEvent>(OnConnectionAttempt);
    }

    public override void OnDisable()
    {
        FirewallEventService.Unsubscribe<ConnectionAttemptEvent>(OnConnectionAttempt);
        Logger.Log("ThresholdAlertPlugin disabled.", LogLevel.INFO);
    }

    private void OnConnectionAttempt(ConnectionAttemptEvent ev)
    {
        var ip    = ev.IpAddress;
        var count = _stats.GetCount(ip);

        var statsCfg = ConfigManager
            .LoadConfig<StatsConfig>(
                _stats.Name,     
                "StatsConfig");  

        var thr   = statsCfg.AlertThreshold;

        if (count >= thr * 2)
            Logger.Log($"[ALERT] {ip} hit critical count {count}", LogLevel.CRITICAL);
    }

    public override IEnumerable<ICommand> GetCommands()
    {
        yield return new AlertCommand(_stats);
    }

    private class AlertCommand : ICommand
    {
        private readonly ConnectionStatsPlugin _stats;
        public AlertCommand(ConnectionStatsPlugin stats) => _stats = stats;

        public string Name        => "alert";
        public string Description => "Checks if IP is in warning or critical zone";
        public string Usage       => "alert <ip>";

        public void Execute(string[] args, IFirewallContext ctx, out string response)
        {
            if (args.Length != 1)
            {
                response = $"Usage: {Usage}";
                return;
            }
            var ip    = args[0];
            var count = _stats.GetCount(ip);
            
            var statsCfg = ThresholdAlertPlugin._instance.ConfigManager
                .LoadConfig<StatsConfig>(
                    _stats.Name,     
                    "StatsConfig");   

            var thr   = statsCfg.AlertThreshold;
            if (count >= thr * 2)        response = $"CRITICAL: {ip} @ {count}";
            else if (count >= thr)       response = $"WARNING: {ip} @ {count}";
            else                         response = $"OK: {ip} @ {count}";
        }
    }
}
```

---

## 🔌 Plugin Firewall API Helpers

---

Plugins derived from `PluginBase` have convenient, protected methods to interact with the core firewall via the `IFirewallAPI`:

### 1. GetBlockedIPs()

```csharp
protected IReadOnlyList GetBlockedIPs()
```

• Returns all IP addresses currently blocked by the firewall.  
• Typical use: show or react to a dynamic blocklist.

Example:

```csharp
var blocked = GetBlockedIPs(); 
Logger.Log($"Currently blocked: {string.Join(", ", blocked)}", LogLevel.INFO);
```

---

### 2. GetWhitelistedIPs()

```csharp
protected IReadOnlyList GetWhitelistedIPs()
```

Returns all IP addresses explicitly whitelisted (never to be blocked).  
• Useful for status dashboards or conditional logic in your plugin.

Example:

```csharp
foreach (var ip in GetWhitelistedIPs()) 
{ 
    // skip processing or notify admin Logger.Log($"Whitelisted: {ip}", LogLevel.DEBUG); 
}
```

---

### 3. GetCommand<TCommand>(out ICommand command)

```csharp
protected void GetCommand (out ICommand command) where TCommand : ICommand
```


• Retrieves a registered command instance of type `TCommand`.  
• `command` will be `null` if not found.  
• Enables introspection or manual execution of other commands.

Example:

```csharp
GetCommand (out var cmd); 

if (cmd != null) 
{
    Logger. Log($"Found command: {cmd. Name}", LogLevel. INFO);
}

```

---

### 4. ProcessCommand<TCommand>(string args)

```csharp
protected void ProcessCommand (string args) where TCommand : ICommand
```

• Executes the `TCommand` with the given argument string.  
• Splits `args` on spaces; the first token must match `TCommand.Name`.  
• Logs automatically via the `IFirewallContext`.

Example:

```csharp
// Unblock IP via built-in command semantics ProcessCommand ("unblock 10.0.0.42");
```

---

These helpers let your plugin focus on business logic—without boilerplate plumbing—and leverage all existing core commands, whitelists, and blocklists in a type-safe, discoverable way.

---

## 🧩 Dependency System

Plugins can declare required and optional dependencies so that the loader can enforce load-order and skip incompatible plugins.

### 1. Decorate with `[PluginDepend]`

```csharp
[PluginDepend("ConnectionStats")]
public class ThresholdAlertPlugin : PluginBase 
{ 
    // ....
}

```

- **RequiredDependencies**  
  Names of plugins that **must** be present.  
  If any are missing, the plugin is skipped and an error is logged.
- **OptionalDependencies**  
  Names of plugins that **may** enhance functionality.  
  Missing optional deps emit a warning but do **not** block loading.

### 2. Loader behavior

The `PluginManger` will, for each discovered plugin:

1. Read its `PluginDependAttribute` (if present).
2. Compare required names against all other plugin `Name`s.
3. Skip on missing required; warn on missing optional.
4. Initialize and schedule `OnEnable` only if dependency checks pass.

---

## ⚙️ Configuration Injection

`PluginBase` automates loading and saving of per-plugin config objects via the `[Config]` attribute.

### 1. Mark a field or property

```csharp
[Config("StatsConfig.yml")] 
public StatsConfig StatsConfig { get; private set; }

// Or

[Config]
public StatsConfig StatsConfig { get; private set; }

```

- `[Config]` can target **public** or **private** fields/properties.
- Optionally specify a file name; otherwise the type’s name is used.

### 2. Auto-wiring in `Initialize`

- On plugin startup, `PluginBase.Initialize(...)` calls `AutoWireConfigs()`.
- It reflects over members with `[Config]` and invokes

```csharp
  var example = ConfigManager.LoadConfig<T>(fileName)
```
- The resulting object is assigned into your field/property.

### 3. Manual helpers

Beyond auto-wiring, you can explicitly:

```csharp
// Load or create a new config instance var cfg = LoadConfig ("MyConfig. yml");
// Update and persist cfg.SomeValue = 42; SaveConfig("MyConfig.yml", cfg);
```

### 4. Storage location

All configs live under your project’s directory in a folder hierarchy:

```markdown
root/
├── Plugins/
│   └── {PluginName}/
│       ├── Config/
│       │   └── {fileName}
│       └── (other plugin assets…)
├── core/
```

- `{PluginName}` is the plugin’s `Name` value.
- `{fileName}` is the config file specified in `[Config]` or passed to `LoadConfig`.


This is managed by the `IPluginConfigManager` implementation (e.g. YAML, encrypted, etc.).

---

## 🧠 Event System

### 📍 Purpose

The `FirewallEventService` allows loosely-coupled communication across the system through events.

### ✅ Key Features

- Subscribe to and unsubscribe from specific event types
- Publish events to all current subscribers
- Thread-safe using `ConcurrentDictionary` and locking

### 📄 Creating an Event

```csharp
public class BlockEvent : EventArgs
{
    public string IpAddress { get; }
    public int? DurationSeconds { get; }

    public BlockEvent(string ipAddress, int? durationSeconds = null)
    {
        IpAddress = ipAddress;
        DurationSeconds = durationSeconds;
    }
}
```
### 💡 Example Usage

```csharp
// Subscribe to an event
FirewallEventService.Instance.Subscribe<BlockEvent>(HandleBlock);

// Unsubscribe from an event
FirewallEventService.Instance.Unsubscribe<BlockEvent>(HandleBlock);

// Publish -- This will call the event and any subsribers will run this
FirewallEventService.Instance.Publish(new BlockEvent("8.8.8.8", 3600));

private void HandleBLockEvent(BlockEvent obj)
{
    LogAction($"IP Address {obj.IpAddress} blocked for {obj.DurationSeconds} seconds.", LogLevel.WARNING);
}
```
---

## 🚀 Firewall Command System

### 📍 Purpose

`CommandManager` provides a clean and flexible way to manage and execute user commands.

### 🧱 Structure

Implements ICommand interface
All commands are registered on initialization
Input is parsed and executed with the relevant logic

### 💡 Registering a Command

This will register any external command with the core Firewall Service.

```csharp
public override IEnumerable<ICommand> GetCommands()
{
    return new List<ICommand>
    {
        new StatsCommand(this),
    };
}
```

### 📄 ICommand Interface

```csharp
public interface ICommand
{
    string Name { get; }
    string Description { get; }
    string Usage { get; }
    void Execute(string[] args, IFirewallContext context, out string response);
}
```

### 💬 Example Command: StatsCommand

```csharp
private class StatsCommand : ICommand
{
        private readonly ConnectionStatsPlugin _parent;
        public StatsCommand(ConnectionStatsPlugin parent) => _parent = parent;

        public string Name        => "stats";
        public string Description => "Shows connection count for an IP";
        public string Usage       => "stats <ip>";

        public void Execute(string[] args, IFirewallContext ctx, out string response)
        {
            if (args.Length != 1)
            {
                response = $"Usage: {Usage}";
                return;
            }
            var ip = args[0];
            var count = _parent.GetCount(ip);
            response = $"Connection attempts for {ip}: {count}";
        } 
}
```

---

Using commands within your project directly or require access? Don't worry, this can be done easily by using
our built-in Command retrieve method.

```csharp
GetCommand<StatsCommand>(out var cmd);

Logger.Log($"[Stats] Registered command {cmd.Name}", LogLevel.INFO);

// Or use this built in method to run the command
ProcessCommand<StatsCommand>("8.8.8.8");
```
---

## Scheduler Service

Every plugin has access to a built-in scheduler via the injected `SchedulerService`. 
Use it to run delayed or recurring tasks without blocking the main thread.

### Scheduler API

- ScheduleOnce(TimeSpan delay, Action action) → Guid
- ScheduleOnce(TimeSpan delay, Func<Task> func) → Guid
- ScheduleRecurring(TimeSpan dueTime, TimeSpan period, Action action) → Guid
- ScheduleRecurring(TimeSpan dueTime, TimeSpan period, Func<Task> func) → Guid
- Cancel(Guid id) → bool
- CancelAll()  

### Schedule Usage Examples

```csharp
SchedulerService.ScheduleOnce(TimeSpan.FromSeconds(25), () =>
{
    Logger.Log("ActionSchedulerService", LogLevel.INFO);
});

SchedulerService.ScheduleRecurring(TimeSpan.FromSeconds(40), TimeSpan.FromSeconds(20), () =>
{
    Logger.Log("ActionSchedulerService", LogLevel.INFO);
});

SchedulerService.ScheduleOnce(TimeSpan.FromSeconds(45), ActionSchedulerService);

private void ActionSchedulerService()
{
    Logger.Log("ActionSchedulerService", LogLevel.INFO);
}
```
Want to manage or cancel a task? This can be done bu passing the GUID of the schedule service and passing it to our cancel task method.

```csharp
Guid taskId = SchedulerService.ScheduleOnce(TimeSpan.FromSeconds(20), () =>
{
    Logger.Log("Log request task", LogLevel.INFO);
});

Guid TaskIdRecurrent = SchedulerService.ScheduleRecurring(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), () =>
{
    Logger.Log("Log request task", LogLevel.INFO);
});

SchedulerService.Cancel(taskId);
SchedulerService.Cancel(TaskIdRecurrent);
```

And if you wish to cancel all task simple call this method.

````csharp
SchedulerService.CancelAll();
````

## Plugin Handler

Every plugin has access to a built-in plugin handler via the injected PluginHandler. 
This can be used to fetch info regarding other plugins and help more with advanced dependency utils.

### Plugin Handler API

- IReadOnlyList<IPlugin> Plugins { get; } → List<IPlugin>

- bool TryGetPlugin(string name, out IPlugin plugin) → bool/IPlugin 

- bool TryGetPlugin<T>(out T plugin) where T : class, IPlugin → bool/IPlugin

### Plugin Handler Usage

```csharp
var pluginExample = PluginHandler.Plugins.FirstOrDefault(p => p.Name == "PluginExample");

PluginHandler.TryGetPlugin("PluginExample", out var pluginExampleTwoInstance);

Logger.Log($"PluginExample: {pluginExample.Name} by: {pluginExample.Author}", LogLevel.INFO);
Logger.Log($"PluginExample: {pluginExampleTwoInstance.Name} by: {pluginExampleTwoInstance.Author}", LogLevel.INFO);
```

Grab all plugins via the active plugin list.

```csharp
foreach (var plugin in PluginHandler.Plugins)
{
    Logger.Log($"Plugin: {plugin.Name} by: {plugin.Author}", LogLevel.INFO);
}
```

If using a direct refference, you can use this approach as well.

```csharp
PluginHandler.TryGetPlugin<ConnectionStatsPlugin>(out var pluginExampleThreeInstance);
Logger.Log($"PluginExample: {pluginExampleThreeInstance.Name} by: {pluginExampleThreeInstance.Author}", LogLevel.INFO);
```

## Authors & Acknowledgements

**Maintainer:**
- GitHub: [@Dragonoid](https://github.com/DraconicKnights)

Contributors are welcome! Please open issues or pull requests on the [project repository](https://github.com/your-org/firewall-api).

---

## Donations & Support

If you find this project useful and would like to support its development, you can:

- View my other Projects: https://github.com/DraconicKnights
- Patreon: https://www.patreon.com/c/DraconicKnight

Every contribution—no matter how small—helps sustain ongoing maintenance, improve features, and cover infrastructure costs.

---

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.




