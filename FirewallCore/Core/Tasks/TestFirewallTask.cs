using DragonUtilities.Enums;
using FirewallCore.Commands;
using FirewallEvent.Events.Core;
using FirewallEvent.Events.EventCalls;

namespace FirewallCore.Core.Tasks;

public class TestFirewallTask : FirewallTask
{
    public override void Tick()
    {
        base.Tick();
        FirewallServiceProvider.Instance.CommandManager.GetCommand<RotateCommand>(out var test);
        if (test != null)
        {
            FirewallServiceProvider.Instance.GetLogger.Log($"Command: {test.Name} works Grab method works", LogLevel.INFO);
        }
        
        FirewallServiceProvider.Instance.CommandManager.GetCommand("rotate", out var command);
        command.Execute(null!, FirewallServiceProvider.Instance, out _);
        if (command != null)
        {
            FirewallServiceProvider.Instance.GetLogger.Log($"Command: {command.Name} works Grab method works", LogLevel.INFO);
        }

        if (FirewallEventService.Instance.TryGetSubscriber<UnblockEvent>(out var subscriber))
        {
            subscriber!.Invoke(new UnblockEvent("9.9.9.9"));
        }
        
        FirewallEventService.Instance.GetSubscribers<BlockEvent>(out var allBlockHandlers);
        allBlockHandlers.TriggerAll(new BlockEvent("9.9.9.9"));
    }
}