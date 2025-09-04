namespace FirewallEvent.Events.EventCalls;

public class ConnectionAttemptEvent : EventArgs
{
    public string IpAddress { get; }
    public DateTime TimeStamp { get; }
    
    public ConnectionAttemptEvent(string ipAddress)
    {
        IpAddress = ipAddress;
        TimeStamp = DateTime.UtcNow;
    }
}