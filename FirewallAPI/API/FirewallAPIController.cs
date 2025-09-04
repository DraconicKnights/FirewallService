using DragonUtilities.Enums;
using FirewallEvent.Events.Core;
using FirewallInterface.Interface;

namespace FirewallAPI.API;

internal class FirewallApiController(IFirewallContext context, ICommandManager commandManager) : IFirewallAPI
{
    public IReadOnlyList<string> GetBlockedIPs()
        => Context.BlockListManager.BlockedIPs.ToList();
    public IReadOnlyList<string> GetWhitelistedIPs()
        => Context.BlockListManager.WhitelistedIPs.ToList();
    private IFirewallContext Context { get; } = context ?? throw new ArgumentNullException(nameof(context));
    private ICommandManager CommandManager { get; } = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
    
    public void GetCommand<TCommand>(out ICommand command) where TCommand : ICommand
    {
        CommandManager.GetCommand<TCommand>(out var commandvalue);
        command = commandvalue!;
    }

    public void ProcessCommand<TCommand>(string args) where TCommand : ICommand
    {
        CommandManager.ProcessCommand<TCommand>(args, context, out var response);
        
        Context.LogAction(response, LogLevel.INFO);
    }
}