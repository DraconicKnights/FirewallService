using DragonUtilities.Enums;
using FirewallInterface.Interface;

namespace FirewallCore.Commands
{
    public class BlockCommand : ICommand
    {
        public string Name => "block";
        public string Description => "Block an IP. Usage: block <ip> [durationInSeconds].";
        public string Usage => "block <ip> [durationInSeconds]. If no duration is provided, the block is permanent.";

        public void Execute(string[] args, IFirewallContext context, out string response)
        {
            if (args.Length < 1)
            {
                response = Usage;
                return;
            }

            string ipToBlock = args[0].Trim();

            // Check if this IP is whitelisted.
            if (FirewallServiceProvider.Whitelist.Contains(ipToBlock))
            {
                context.LogAction($"Manual block request for whitelisted IP: {ipToBlock} was ignored.", LogLevel.INFO);
                response = $"IP {ipToBlock} is whitelisted and cannot be blocked.";
                return;
            }
            
            // If a duration is provided, use it.
            if (args.Length >= 2 && int.TryParse(args[1].Trim(), out int duration))
            {
                context.ManualBlockIP(ipToBlock, duration);
                response = $"IP {ipToBlock} has been blocked for {duration} seconds.";
            }
            else
            {
                context.ManualBlockIP(ipToBlock);
                response = $"IP {ipToBlock} has been blocked.";
            }
        }
    }
}