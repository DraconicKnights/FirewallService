using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FirewallCore.Core.Config;

namespace FirewallCore;

/// <summary>
/// Provides extension methods for the configuration of the firewall system.
/// </summary>
public static class FirewallConfigExtensions
{
    /// <summary>
    /// A default header comment used as a description or explanation in the saved firewall configuration files.
    /// This string contains details regarding the firewall configuration setup and supported formats.
    /// </summary>
    public const string DefaultHeader = 
            "Firewall configuration\n" +
            " - Adjust thresholds as needed\n" +
            " - Supported formats: JSON or YAML\n";

    /// <summary>
    /// Applies default configuration values to the provided <see cref="FirewallConfig"/> instance
    /// for any properties that are uninitialized or have default values.
    /// </summary>
    /// <param name="cfg">
    /// The <see cref="FirewallConfig"/> instance to which the default values will be applied.
    /// </param>
    /// <returns>
    /// A boolean value indicating whether any changes were made to the configuration.
    /// Returns <c>true</c> if defaults were applied, otherwise <c>false</c>.
    /// </returns>
    public static bool ApplyDefaults(this FirewallConfig cfg)
    {
            var dirty = false;

            if (cfg.FirewallSettings.ThresholdAttempts == 0)
            {
                cfg.FirewallSettings.ThresholdAttempts = 40;
                dirty = true;
            }

            if (cfg.FirewallSettings.ThresholdSeconds == 0)
            {
                cfg.FirewallSettings.ThresholdSeconds = 30;
                dirty = true;
            }

            if (cfg.FirewallSettings.DefaultBlockDurationSeconds == 0)
            {
                cfg.FirewallSettings.DefaultBlockDurationSeconds = 20 * 60;
                dirty = true;
            }

            if (cfg.Logging.LogRotationLineCount == 0)
            {
                cfg.Logging.LogRotationLineCount = 200;
                dirty = true;
            }

            if (cfg.Logging.MaxLogArchives == 0)
            {
                cfg.Logging.MaxLogArchives = 5;
                dirty = true;
            }

            if (string.IsNullOrWhiteSpace(cfg.Logging.SecureExportPath))
            {
                cfg.Logging.SecureExportPath = "__CREATE_SECURE_LOG_PATH__";
                dirty = true;
            }

            if (cfg.FirewallSettings.ssh.Seconds == 0)
            {
                cfg.FirewallSettings.ssh.Seconds = 60;
                dirty = true;
            }

            if (cfg.FirewallSettings.ssh.Hitcount == 0)
            {
                cfg.FirewallSettings.ssh.Hitcount = 5;
            }
            
            var taskList = cfg.FirewallSettings.FirewallTasks.Tasks;
            var existingNames = new HashSet<string>(taskList.Select(t => t.Name));

            var allTaskTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsClass
                            && !t.IsAbstract
                            && t.Name.EndsWith("Task"));
            
            foreach (var type in allTaskTypes)
            {
                if (!existingNames.Contains(type.Name))
                {
                    // add new entry, defaulting to enabled
                    taskList.Add(new FirewallTaskToggle {
                        Name = type.Name,
                        Enabled = true
                        });
                    dirty = true;
                }
            }

            if (string.IsNullOrWhiteSpace(cfg.FirewallSettings.Certificate.Path))
            {
                cfg.FirewallSettings.Certificate.Path = "certificate.pfx";
                dirty = true;
            }

            if (string.IsNullOrWhiteSpace(cfg.FirewallSettings.Certificate.Password))
            {
                cfg.FirewallSettings.Certificate.Password = "__GENERATE__";
                dirty = true;
            }

            if (string.IsNullOrWhiteSpace(cfg.FirewallSettings.Crypto.Key))
            {
                cfg.FirewallSettings.Crypto.Key = "__GENERATE__";
                dirty = true;
            }

            if (string.IsNullOrWhiteSpace(cfg.FirewallSettings.Crypto.Iv))
            {
                cfg.FirewallSettings.Crypto.Iv = "__GENERATE__";
                dirty = true;
            }
        

            return dirty;
    }
}