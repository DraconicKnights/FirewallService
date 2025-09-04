using FirewallInterface.Interface;

namespace FirewallCore.Commands;

public class RotateCommand : ICommand
{
    public string Name => "rotate";
    public string Description => "Force log rotation.";
    public string Usage => "rotate";
    public void Execute(string[] args, IFirewallContext context, out string response)
    {
        context.RotateLogs();
        response = string.Empty;
    }

}