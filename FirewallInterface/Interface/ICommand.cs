
namespace FirewallInterface.Interface;

public interface ICommand
{
    string Name { get; }
    string Description { get; }
    string Usage { get; }
    void Execute(string[] args, IFirewallContext context, out string response);
}