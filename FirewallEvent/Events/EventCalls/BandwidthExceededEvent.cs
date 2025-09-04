namespace FirewallEvent.Events.EventCalls;

public class BandwidthExceededEvent : EventArgs
{
    public string InterfaceName         { get; }
    public long   BytesPerSecond        { get; }
    public long   ThresholdBytesPerSecond { get; }
    public DateTime Timestamp          { get; }

    public BandwidthExceededEvent(string iface, long bps, long threshold)
    {
        InterfaceName           = iface;
        BytesPerSecond          = bps;
        ThresholdBytesPerSecond = threshold;
        Timestamp               = DateTime.UtcNow;
    }
}