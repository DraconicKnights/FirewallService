using DragonUtilities.Enums;
using FirewallEvent.Events.Core;
using FirewallEvent.Events.EventCalls;

namespace FirewallCore.Core.Tasks;

public class PortScanDetectionTask : FirewallTask
{
    private readonly int _portThreshold;
    private readonly int _blockDurationSeconds;
    private readonly Dictionary<string, HashSet<int>> _seenPorts = new();

    public PortScanDetectionTask(int portThreshold = 50, int blockDurationSeconds = 600)
    {
        _portThreshold = portThreshold;
        _blockDurationSeconds = blockDurationSeconds;
    }

    public override void Initialize()
    {
        _seenPorts.Clear();
    }

    public override void StartTask() { }

    public override void Tick()
    {
        foreach (var kv in _seenPorts)
        {
            string ip = kv.Key;
            int uniquePorts = kv.Value.Count;
            if (uniquePorts > _portThreshold)
            {
                FirewallEventService.Instance.Publish(new PortScanDetectedEvent(ip, [uniquePorts]));
                FirewallServiceProvider.Instance.LogAction(
                    $"[PortScan] {ip} probed {uniquePorts} ports (limit: {_portThreshold}). Blocking for {_blockDurationSeconds}s",
                    LogLevel.WARNING);
                FirewallServiceProvider.Instance.ManualBlockIP(ip, _blockDurationSeconds);
            }
        }
        _seenPorts.Clear();
    }

    public override void Shutdown()
    {
        _seenPorts.Clear();
    }

    public void OnPortProbe(string ip, int port)
    {
        if (!_seenPorts.TryGetValue(ip, out var set))
            _seenPorts[ip] = set = new HashSet<int>();
        set.Add(port);
    }

}