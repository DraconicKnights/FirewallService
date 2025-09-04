using DragonUtilities.Enums;
using FirewallInterface.Interface;

namespace FirewallCore.Commands;

public class ListCommand : ICommand
{
    public string Name => "list";
    public string Description => "Display all currently blocked IP addresses.";
    public string Usage => "list";

    public void Execute(string[] args, IFirewallContext context, out string response)
    {
        string blockedIPs = context.GetBlockedAddressesResponse();
        context.LogAction("List command executed.", LogLevel.INFO);

        response = string.IsNullOrEmpty(blockedIPs)
            ? "No blocked IP addresses."
            : blockedIPs;
    }

}