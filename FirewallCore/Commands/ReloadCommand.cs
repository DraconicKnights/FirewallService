using FirewallInterface.Interface;

namespace FirewallCore.Commands;

public class ReloadCommand : ICommand
{
    public string Name => "reload";
    public string Description => "Reload all firewall rules.";
    public string Usage => "reload";
    public void Execute(string[] args, IFirewallContext context, out string response)
    {
        context.ReloadFirewallRules();
        response = string.Empty;
    }

}