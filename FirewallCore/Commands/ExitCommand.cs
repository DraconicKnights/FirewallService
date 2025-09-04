using FirewallInterface.Interface;

namespace FirewallCore.Commands;

public class ExitCommand : ICommand
{
    public string Name => "exit";
    public string Description => "Shutdown the log service gracefully.";
    public string Usage => "exit";
    public void Execute(string[] args, IFirewallContext context, out string response)
    {
        response = "Shutting down...";
        
        FirewallServiceProvider.Instance.Shutdown();
        
        Environment.Exit(0);
    }

}