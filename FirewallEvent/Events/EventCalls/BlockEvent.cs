
namespace FirewallEvent.Events.EventCalls;

public class BlockEvent : EventArgs
{
    public string IpAddress { get; }
    public int? DurationSeconds { get; }
    
    public BlockEvent(string ipAddress, int? durationSeconds = null)
    {
        IpAddress = ipAddress;
        DurationSeconds = durationSeconds;
    }
}