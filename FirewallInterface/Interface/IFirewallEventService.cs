namespace FirewallInterface.Interface;

public interface IFirewallEventService
{
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : EventArgs;
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : EventArgs;
    void Publish<TEvent>(TEvent eventData) where TEvent : EventArgs;
    void GetSubscribers<TEvent>(out List<Action<TEvent>> handlers) where TEvent : EventArgs;
    bool TryGetSubscriber<TEvent>(out Action<TEvent>? handler) where TEvent : EventArgs;
}