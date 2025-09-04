using System.Text;
using DragonUtilities.Enums;
using FirewallInterface.Interface;

namespace FirewallCore.Commands;

public class IpHistoryCommand : ICommand
{
    public string Name => "ip-history";
    public string Description => "Displays the history of an IP address.";
    public string Usage => "ip-history <ipGuid>";

    public void Execute(string[] args, IFirewallContext context, out string response)
    {
        if (args.Length < 1 || !Guid.TryParse(args[0], out var ipGuid))
        {
            response = "Error: invalid GUID. " + Usage;
            return;
        }

        try
        {
            var history = context.GetHistory(ipGuid);
            if (!history.Any())
            {
                response = $"No history found for GUID {ipGuid}.";
                return;
            }

            // Build response text
            var sb = new StringBuilder();
            foreach (var (time, message) in history)
            {
                sb.AppendLine($"{time:yyyy-MM-dd HH:mm:ss} - {message}");
            }

            response = sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            context.LogAction($"Failed to retrieve history for {ipGuid}: {ex.Message}", LogLevel.ERROR);
            response = $"Error retrieving history: {ex.Message}";
        }
    }
}