using DragonUtilities.Enums;
using FirewallInterface.Interface;

namespace FirewallCore.Commands;

public class UnblockCommand : ICommand
{
    public string Name => "unblock";
    public string Description => "Unblock an IP.";
    public string Usage => "unblock <ip>.";
    public void Execute(string[] args, IFirewallContext context, out string response)
    {
        if (args.Length < 1)
        {
            context.LogAction("Usage: unblock <ip>", LogLevel.INFO);
            response = Usage;
        }
        else
        {
            context.UnblockIP(args[0], context.LogAction);
            response = $"IP {args[0]} has been unblocked.";
        }
    }

}