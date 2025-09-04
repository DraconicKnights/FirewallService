using FirewallInterface.Interface;

namespace FirewallCore.Commands;

public class ClearCommand : ICommand
{
    public string Name => "clear";
    public string Description => "Clear the console display.";
    public string Usage => "clear";
    public void Execute(string[] args, IFirewallContext context, out string response)
    {
        Console.Clear();
        response = "Console cleared.";
    }

}