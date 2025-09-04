using FirewallCore.Core;
using FirewallCore.Core.Config;
using FirewallCore.Utils;

namespace FirewallCore;

/// <summary>
/// The entry point of the application that initializes and starts the log system service.
/// </summary>
/// <remarks>
/// This class performs essential setup operations such as configuration management
/// and service initialization. It is responsible for managing application lifecycle events,
/// such as handling Ctrl+C cancellation events for graceful shutdown of the service.
/// </remarks>
/// <example>
/// This program uses <see cref="FileManager{T}"/> to manage configuration files and
/// <see cref="FirewallServiceProvider"/> to initialize and run the log service system.
/// </example>
public class Program
{
    /// <summary>
    /// The entry point of the log application. Executes the main application workflow, initializes the configuration,
    /// and starts the firewall service log processing.
    /// </summary>
    /// <param name="args">An array of command-line arguments passed to the application.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task Main(string[] args)
    {
        const string configFile = "firewallconfig";
        
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string configFolder = Path.Combine(baseDir, nameof(FirewallConfig));

        var fileManager = new FileManager<FirewallConfig>(
            crypto: null,
            defaultFormat: FileFormat.Yaml);

        FirewallConfig config;
        try
        {
            config = await fileManager.LoadAsync(configFile);
        }
        catch (FileNotFoundException)
        {
            config = new FirewallConfig();
            config.ApplyDefaults();
            await fileManager.SaveAsync(
                configFile,
                config,
                secure: false,
                forceFormat: FileFormat.Yaml,
                headerComment: FirewallConfigExtensions.DefaultHeader);
        }
        
        var dirty = false;
        
        dirty |= config.ApplyDefaults();
        dirty |= config.EnsureCryptoAndCertificate(baseDir);
        dirty |= config.EnsureSecureLoggingPaths(baseDir);

        if (dirty)
        {
            await fileManager.SaveAsync(
                configFile,
                config,
                secure: false,
                forceFormat: FileFormat.Yaml,
                headerComment: FirewallConfigExtensions.DefaultHeader);
        }

        var firewallService = new FirewallServiceProvider(config);
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            firewallService.LogAction("Cancellation requested…");
            cts.Cancel();
        };

        await firewallService.StartFirewallServiceAsync(cts.Token);

    }

}