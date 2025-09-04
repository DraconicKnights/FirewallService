namespace FirewallEvent.Events.EventCalls;

public class UnblockEvent : EventArgs
{
      public string IpAddress { get; }
      
      public UnblockEvent(string ipAddress)
      { 
            IpAddress = ipAddress;
      }
}