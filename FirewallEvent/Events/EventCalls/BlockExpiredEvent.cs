namespace FirewallEvent.Events.EventCalls;

public class BlockExpiredEvent : EventArgs
{
    public string IpAddress { get; }
    public DateTime ExpiredAt { get; }

    public BlockExpiredEvent(string ip)
    {
        IpAddress = ip;
        ExpiredAt = DateTime.UtcNow;
    }

}