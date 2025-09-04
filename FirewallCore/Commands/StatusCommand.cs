using DragonUtilities.Enums;
using FirewallInterface.Interface;

namespace FirewallCore.Commands;

public class StatusCommand : ICommand
{
    public string Name => "status";
    public string Description => "Display status of connection attempts.";
    public string Usage => "status";
    public void Execute(string[] args, IFirewallContext context, out string response)
    {
        string status = context.GetStatusResponse();
        context.LogAction("Status command executed.", LogLevel.INFO);

        response = status;
    }

}