namespace FirewallEvent.Events.EventCalls;

public class GeoBlockEvent : EventArgs
{
    public string IpAddress { get; }
    public string Country { get; }
    public DateTime TimeStamp { get; }

    public GeoBlockEvent(string ip, string country)
    {
        IpAddress = ip;
        Country = country;
        TimeStamp = DateTime.UtcNow;
    }
}