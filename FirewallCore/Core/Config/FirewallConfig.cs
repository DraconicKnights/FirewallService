using System.ComponentModel;
using System.Text.Json.Serialization;

namespace FirewallCore.Core.Config;

public class FirewallConfig
{
    [Description("Core Firewall Settings")]
    [JsonPropertyName("Firewall")]
    public FirewallSettings FirewallSettings { get; set; } = new FirewallSettings();
    
    [Description("Logging Settings")]
    [JsonPropertyName("Logging")]
    public LoggingConfig Logging { get; set; } = new LoggingConfig();
}

public class LoggingConfig
{
    [Description("Log Rotation Line Count")]
    public int LogRotationLineCount { get; set; }
    [Description("Max Log Archives that can be stored")]
    public int MaxLogArchives { get; set; }
    [Description("Secure Export Path")]
    public string SecureExportPath { get; set; }
    [Description("Allow plaintext current log. Please note this is not secure and with setting this to enabled you are taken risk with this data being exposed.")]
    public bool AllowPlaintextCurrentLog { get; set; } = false;
}

public class FirewallSettings
{
    [Description("Max attempts before blocking IP address")]
    public int ThresholdAttempts { get; set; }
    [Description("Max seconds before blocking IP address")]
    public int ThresholdSeconds { get; set; }
    [Description("Default block duration in seconds")]
    public int DefaultBlockDurationSeconds { get; set; }
    
    [Description("Allow plaintext command connections (no TLS). Only recommended for local/dev.")]
    public bool AllowPlainTextCommands { get; set; } = false;
    
    [Description("Allow plugins to be loaded.")]
    public bool PluginsEnabled { get; set; } = true;
    [Description("SSH Settings")]
    public SSHConfig ssh { get; set; } = new SSHConfig();
    [Description("Firewall Task Manager")]
    public FirewallTasksConfig FirewallTasks { get; set; } = new FirewallTasksConfig();
    
    [Description("Certificate Settings")]
    public CertificateConfig Certificate { get; set; } = new CertificateConfig();
    [Description("Crypto Settings")]   
    public CyrptoConfig Crypto { get; set; } = new CyrptoConfig();
}

public class CertificateConfig
{
    public string Path { get; set; }
    public string Password { get; set; }
}

public class CyrptoConfig
{
    public string Key { get; set; }
    public string Iv { get; set; }
}

public class SSHConfig
{
    public int Port { get; set; }
    public int Seconds {get; set;}
    public int Hitcount {get; set;}
}

public class FirewallTasksConfig
{
    public List<FirewallTaskToggle> Tasks { get; set; } = new List<FirewallTaskToggle>();
}

public class FirewallTaskToggle
{
    public string Name { get; set; }
    public bool Enabled { get; set; }
}
