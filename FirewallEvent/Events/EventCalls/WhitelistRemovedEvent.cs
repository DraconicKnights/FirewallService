namespace FirewallEvent.Events.EventCalls;

public class WhitelistRemovedEvent : EventArgs
{
    public string IpAddress { get; }
    public DateTime TimeStamp { get; }

    public WhitelistRemovedEvent(string ip)
    {
        IpAddress = ip;
        TimeStamp = DateTime.UtcNow;
    }
}