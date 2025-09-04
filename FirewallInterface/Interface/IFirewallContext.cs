using DragonUtilities.Enums;

namespace FirewallInterface.Interface;

public interface IFirewallContext
{
    /// <summary>
    /// Command registry.
    /// </summary>
    ICommandManager CommandManager { get; }
    IBlockListManager BlockListManager { get; }

    /// <summary>
    /// Log an action (same as FirewallService.LogAction).
    /// </summary>
    void LogAction(string message, LogLevel level = LogLevel.INFO);

    /// <summary>
    /// Export & encrypt all logs to the given filename.
    /// </summary>
    void ExportLogs(string fileName);
    
    string ReadExportedLogs(string f);

    /// <summary>
    /// Manually block an IP (durationSeconds == null → permanent).
    /// </summary>
    void ManualBlockIP(string ip, int? durationSeconds = null);

    /// <summary>
    /// A quick whitelist‐check helper.
    /// </summary>
    bool IsWhitelisted(string ip);
    string GetBlockedAddressesResponse();
    string GetStatusResponse();
    
    /// <summary>
    /// Re-deploys all firewall rules (presets, defaults, custom, scheduled blocks).
    /// </summary>
    void ReloadFirewallRules();
    
    void RotateLogs();
    void UnblockIP(string ip, Action<string, LogLevel> logAction);
    void UnblockAll();

    public Guid GetIdentifierForIp(string ip);
    
    /// <summary>
    /// Gets the chronological history of events for the given IP GUID.
    /// </summary>
    IEnumerable<(DateTime Time, string Message)> GetHistory(Guid ipGuid);

    /// <summary>
    /// Clears stored history for the given IP GUID.
    /// </summary>
    void ClearHistory(Guid ipGuid);

    /// <summary>
    /// Gets basic stats: total attempts, recent failures, and last seen timestamp.
    /// </summary>
    (int totalAttempts, int recentFails, DateTime lastSeen) GetStatsForGuid(Guid ipGuid);

    void AddTag(Guid ipGuid, string tag);

    void RemoveTag(Guid ipGuid, string tag);

    IEnumerable<string> GetTags(Guid ipGuid);
    
    /// <summary>
    /// Adds a comment to the given IP GUID.
    /// </summary>
    void AddComment(Guid ipGuid, string comment);

    /// <summary>
    /// Retrieves all comments for the given IP GUID.
    /// </summary>
    IEnumerable<(DateTime Time, string Comment)> GetComments(Guid ipGuid);
}