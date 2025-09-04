using DragonUtilities.Enums;
using FirewallInterface.Interface;

namespace FirewallCore.Commands
{
    public class WhitelistCommand : ICommand
    {
        public string Name => "whitelist";
        public string Description => "Adds or removes an IP address from the whitelist.";
        public string Usage => "whitelist add <ip> OR whitelist remove <ip>";

        public void Execute(string[] args, IFirewallContext context, out string response)
        {
            // Expect exactly two arguments: the action ("add" or "remove") and the IP address.
            if (args.Length != 2)
            {
                context.LogAction("Usage: whitelist add <ip> OR whitelist remove <ip>", LogLevel.INFO);
                response = Usage;
                return;
            }

            string action = args[0].Trim().ToLower();
            string ip = args[1].Trim();

            // Get the block list manager (which handles whitelist file operations).
            var manager = context.BlockListManager;

            // Handle the add command.
            if (action == "add")
            {
                if (FirewallServiceProvider.Whitelist.Contains(ip))
                {
                    context.LogAction($"IP {ip} is already whitelisted.", LogLevel.INFO);
                    response = $"IP {ip} is already whitelisted.";
                    return;
                }
                
                FirewallServiceProvider.Whitelist.Add(ip);
                manager.Add(ip);
                // Append the new IP to the whitelist file.
                File.AppendAllLines(manager.WhitelistPath, new[] { ip });

                context.LogAction($"IP {ip} has been added to the whitelist.", LogLevel.INFO);
                response = $"IP {ip} has been added to the whitelist.";
            }
            // Handle the remove command.
            else if (action == "remove")
            {
                if (!FirewallServiceProvider.Whitelist.Contains(ip))
                {
                    context.LogAction($"IP {ip} is not in the whitelist.", LogLevel.INFO);
                    response = $"IP {ip} is not in the whitelist.";
                    return;
                }

                FirewallServiceProvider.Whitelist.Remove(ip);
                manager.Remove(ip);

                // Read the whitelist file, filter out the IP, and write back.
                var lines = File.ReadAllLines(manager.WhitelistPath)
                                .Where(line => line.Trim() != ip)
                                .ToList();
                File.WriteAllLines(manager.WhitelistPath, lines);

                context.LogAction($"IP {ip} has been removed from the whitelist.", LogLevel.INFO);
                response = $"IP {ip} has been removed from the whitelist.";
            }
            else
            {
                context.LogAction("Usage: whitelist add <ip> OR whitelist remove <ip>", LogLevel.INFO);
                response = "Usage: whitelist add <ip> OR whitelist remove <ip>";
            }
        }
    }
}