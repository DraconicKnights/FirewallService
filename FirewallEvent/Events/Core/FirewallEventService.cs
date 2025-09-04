using System.Collections.Concurrent;
using FirewallInterface.Interface;

namespace FirewallEvent.Events.Core;

public class FirewallEventService : IFirewallEventService
{
    // Singleton instance for FirewallEventService
    private static readonly Lazy<FirewallEventService> _instance =
        new Lazy<FirewallEventService>(() => new FirewallEventService());

    public static FirewallEventService Instance => _instance.Value;
    
    private Action<Delegate, Exception> _errorReporter;

    private FirewallEventService()
    {
        _errorReporter = (handler, ex) =>
        {
            var m    = handler.Method;
            var type = m.DeclaringType?.FullName     ?? "<unknown type>";
            var asm  = m.DeclaringType?.Assembly.GetName().Name ?? "<unknown asm>";
            var name = m.Name;

            var boxLines = new[]
            {
                "┌─ Event Handler Error ──────────────────────────┐",
                $"│ Assembly : {asm}",
                $"│ Type     : {type}",
                $"│ Method   : {name}()",
                $"│ Exception: {ex.GetType().Name}: {ex.Message}",
                "└───────────────────────────────────────────────┘"
            };

            // 3) Use your plugin‐aware logger
            LogError(string.Join(Environment.NewLine, boxLines));
        };
    }
    
    private readonly ConcurrentDictionary<Type, ConcurrentBag<Delegate>> _subscribers = new();

    // Subscribe to an event of type TEvent.
    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : EventArgs
    {
        var bag = _subscribers.GetOrAdd(typeof(TEvent), _ => new ConcurrentBag<Delegate>());
        bag.Add(handler);
    }

    // Unsubscribe from an event of type TEvent.
    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : EventArgs
    {
        if (_subscribers.TryGetValue(typeof(TEvent), out var bag))
        {
            // ConcurrentBag<T> has no Remove, so you’d need to rebuild a bag:
            var newBag = new ConcurrentBag<Delegate>(bag.Except(new[] { handler }));
            _subscribers[typeof(TEvent)] = newBag;
        }
    }

    // Publish an event of type TEvent.
    public void Publish<TEvent>(TEvent eventData) where TEvent : EventArgs
    {
        if (_subscribers.TryGetValue(typeof(TEvent), out var subscribers))
        {
            Delegate[] subscribersSnapshot;
            lock (subscribers)
            {
                subscribersSnapshot = subscribers.ToArray();
            }

            foreach (var subscriber in subscribersSnapshot)
            {
                if (subscriber is Action<TEvent> action)
                {
                    try
                    {
                        action(eventData);
                    }
                    catch (Exception e)
                    {
                        _errorReporter(action, e);
                    }
                }
            }
        }
    }
    
    public void GetSubscribers<TEvent>(out List<Action<TEvent>> handlers) where TEvent : EventArgs
    {
        if (_subscribers.TryGetValue(typeof(TEvent), out var list))
        {
            lock (list)
            {
                handlers = list.OfType<Action<TEvent>>().ToList();
            }
        }
        else
        {
            handlers = new List<Action<TEvent>>();
        }
    }

    public bool TryGetSubscriber<TEvent>(out Action<TEvent>? handler) where TEvent : EventArgs
    {
        handler = null;
        if (_subscribers.TryGetValue(typeof(TEvent), out var list))
        {
            lock (list)
            {
                handler = list.OfType<Action<TEvent>>().FirstOrDefault();
            }
        }
        return handler is not null;
    }

    private void LogError(string message) => Console.Error.WriteLine(message);

}

public static class EventExtensions
{
    /// <summary>
    /// Invoke every handler in the sequence with the provided event.
    /// </summary>
    public static void TriggerAll<TEvent>(
        this IEnumerable<Action<TEvent>> handlers,
        TEvent evt
    ) where TEvent : EventArgs
    {
        if (handlers == null) return;
        foreach (var h in handlers)
        {
            try
            {
                h(evt);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in event handler: {ex}");
            }
        }
    }
}