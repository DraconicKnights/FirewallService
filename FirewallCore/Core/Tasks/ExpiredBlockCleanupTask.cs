using DragonUtilities.Enums;
using FirewallEvent.Events.Core;
using FirewallEvent.Events.EventCalls;

namespace FirewallCore.Core.Tasks;

public class ExpiredBlockCleanupTask : FirewallTask
{
    private int _tickCount;
    private const int TicksBetweenRuns = 30; 
    
    public override void Initialize()
    {
        _tickCount = 0;
        FirewallServiceProvider.Instance.LogAction("ExpiredBlockCleanupTask Initialized", LogLevel.DEBUG);
    }
    
    public override void StartTask()
    {
        FirewallServiceProvider.Instance.LogAction("ExpiredBlockCleanupTask Started", LogLevel.DEBUG);
    }

    public override void Tick()
    {
        if (++_tickCount < TicksBetweenRuns)
            return;

        _tickCount = 0;

        var expired = FirewallServiceProvider.Instance.DatabaseManager.RemoveExpiredBlockedIPs();

        foreach (var addr in expired)
        {
            if (FirewallServiceProvider.BlockedIPs.Remove(addr.IP, out var time))
            {
                FirewallEventService.Instance.Publish(new BlockExpiredEvent(addr.IP));
                
                FirewallServiceProvider.Instance.IptablesManager
                    .UnblockIP(addr.IP, FirewallServiceProvider.Instance.LogAction);
                FirewallServiceProvider.Instance.LogAction($"Auto-unblocked expired IP {addr.IP}", LogLevel.INFO);
            }
        }
    }

    public override void Shutdown()
    {
        FirewallServiceProvider.Instance.LogAction("ExpiredBlockCleanupTask Shutdown", LogLevel.INFO);
    }
}