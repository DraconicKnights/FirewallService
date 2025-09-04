namespace FirewallEvent.Events.EventCalls;

public class RateLimitExceededEvent : EventArgs
{
    public string IpAddress { get; }
    public int    Limit     { get; }
    public int    Hits      { get; }
    public DateTime Timestamp { get; }

    public RateLimitExceededEvent(string ip, int limit, int hits)
    {
        IpAddress = ip;
        Limit     = limit;
        Hits       = hits;
        Timestamp  = DateTime.UtcNow;
    }
}