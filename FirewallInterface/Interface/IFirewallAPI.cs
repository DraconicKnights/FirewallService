
namespace FirewallInterface.Interface;

public interface IFirewallAPI
{
    void GetCommand<TCommand>(out ICommand command) where TCommand : ICommand;
    void ProcessCommand<TCommand>(string args) where TCommand : ICommand;
    IReadOnlyList<string> GetBlockedIPs();
    IReadOnlyList<string> GetWhitelistedIPs();
    
}