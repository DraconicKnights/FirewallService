using DragonUtilities.Enums;
using FirewallInterface.Interface;

namespace FirewallCore.Commands;

public class InfoCommand : ICommand
{
    public string Name => "info";
    public string Description => "Displays detailed firewall statistics.";
    public string Usage => "info";

    public void Execute(string[] args, IFirewallContext context, out string response)
    {
        int monitoredIPs = FirewallServiceProvider.IpAttempts.Count;
        int blockedIPs = FirewallServiceProvider.BlockedIPs.Count;
        context.LogAction($"Monitored IPs: {monitoredIPs}", LogLevel.INFO);
        context.LogAction($"Blocked IPs: {blockedIPs}", LogLevel.INFO);
        
        // Return the detailed information to the client.
        response = $"Monitored IPs: {monitoredIPs}\nBlocked IPs: {blockedIPs}";
    }

}