namespace FirewallEvent.Events.EventCalls;

public class PortScanDetectedEvent : EventArgs
{
    public string IpAddress { get; }
    public IEnumerable<int> Ports { get; }
    public DateTime DetectedAt { get; }

    public PortScanDetectedEvent(string ip, IEnumerable<int> ports)
    {
        IpAddress = ip;
        Ports     = ports;
        DetectedAt = DateTime.UtcNow;
    }
}