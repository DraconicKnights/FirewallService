using System.Diagnostics;
using DragonUtilities.Enums;
using FirewallCore.Data;
using FirewallEvent.Events.Core;
using FirewallEvent.Events.EventCalls;

namespace FirewallCore.Core;

public class IptablesManager
{
    public void ExecuteCommand(string command)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "/sbin/iptables",
            Arguments = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        try
        {
            using var process = Process.Start(psi);
            string errorOutput = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                FirewallServiceProvider.Instance.LogAction($"Failed to execute iptables command: {command}. Error: {errorOutput}", LogLevel.ERROR);
            }
        }
        catch (Exception ex)
        {
            FirewallServiceProvider.Instance.LogAction($"Failed to execute iptables command: {command}. Error: {ex.Message}", LogLevel.ERROR);
        }
    }
    
    public void BlockIP(string ip, Action<string, LogLevel> logAction)
        => BlockIP(ip, logAction, FirewallServiceProvider.DefaultBlockDurationSeconds);

    public void BlockIP(string ip, Action<string, LogLevel> logAction, int autoUnblockDurationSeconds)
    {
        if (FirewallServiceProvider.BlockedIPs.ContainsKey(ip))
            return;

        ExecuteCommand($"-I INPUT 1 -s {ip} -j DROP");
        
        logAction?.Invoke($"Blocked IP: {ip}", LogLevel.WARNING);
        FirewallServiceProvider.BlockedIPs[ip] = DateTime.Now;
        
        var addressData = new BlockedAddress(ip, DateTime.Now, autoUnblockDurationSeconds);
        FirewallServiceProvider.Instance.DatabaseManager.InsertBlockedIP(addressData);
        
        FirewallEventService.Instance.Publish(new BlockEvent(ip, autoUnblockDurationSeconds));
    }

    public void UnblockIP(string ip, Action<string, LogLevel> logAction)
    {
        if (!FirewallServiceProvider.BlockedIPs.ContainsKey(ip))
        {
            logAction?.Invoke($"Skipped unblocking {ip}: already unblocked", LogLevel.INFO);
            return;
        }
        
        ExecuteCommand($"-D INPUT -s {ip} -j DROP");
        
        FirewallEventService.Instance.Publish(new UnblockEvent(ip));
        
        logAction?.Invoke($"Unblocked IP: {ip}", LogLevel.WARNING);
        FirewallServiceProvider.BlockedIPs.Remove(ip, out _);
        FirewallServiceProvider.Instance.DatabaseManager.DeleteBlockedIP(ip);
    }
}