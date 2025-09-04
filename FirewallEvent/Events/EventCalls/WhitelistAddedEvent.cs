namespace FirewallEvent.Events.EventCalls;

public class WhitelistAddedEvent : EventArgs
{
    public string IpAddress { get; }
    public DateTime TimeStamp { get; }

    public WhitelistAddedEvent(string ip)
    {
        IpAddress = ip;
        TimeStamp = DateTime.UtcNow;
    }
}