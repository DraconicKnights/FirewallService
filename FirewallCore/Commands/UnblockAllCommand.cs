using DragonUtilities.Enums;
using FirewallInterface.Interface;

namespace FirewallCore.Commands;

public class UnblockAllCommand : ICommand
{
    public string Name => "unblockall";
    public string Description => "Unblocks all currently blocked IP addresses.";
    public string Usage => "unblockall";

    public void Execute(string[] args, IFirewallContext context, out string response)
    {
        // Make a copy of keys for safe removal.
        var blockedIPs = FirewallServiceProvider.BlockedIPs.Keys.ToList();
        foreach (var ip in blockedIPs)
        {
            context.UnblockIP(ip, context.LogAction);
            FirewallServiceProvider.BlockedIPs.Remove(ip, out _);
        }
        context.LogAction("All IP addresses have been unblocked.", LogLevel.INFO);
        response = "All IP addresses have been unblocked.";
    }

}