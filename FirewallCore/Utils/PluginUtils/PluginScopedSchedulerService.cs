using System.Collections.Concurrent;
using FirewallInterface.Interface;

namespace FirewallCore.Utils;

internal class PluginScopedSchedulerService : ISchedulerService
{
    private readonly ISchedulerService _inner;
    private readonly ConcurrentDictionary<Guid, byte> _myJobs = new();
    
    public PluginScopedSchedulerService(ISchedulerService inner) => _inner = inner;

    public Guid ScheduleOnce(TimeSpan delay, Action action)
        => Track(_inner.ScheduleOnce(delay, action));

    public Guid ScheduleOnce(TimeSpan delay, Func<Task> func)
        => Track(_inner.ScheduleOnce(delay, func));

    public Guid ScheduleOnce<TState>(TimeSpan delay, Action<TState> action, TState state)
        => Track(_inner.ScheduleOnce(delay, action, state));

    public Guid ScheduleOnce<TState>(TimeSpan delay, Func<TState, Task> func, TState state)
        => Track(_inner.ScheduleOnce(delay, func, state));

    public Guid ScheduleRecurring(TimeSpan dueTime, TimeSpan period, Action action)
        => Track(_inner.ScheduleRecurring(dueTime, period, action));

    public Guid ScheduleRecurring(TimeSpan dueTime, TimeSpan period, Func<Task> func)
        => Track(_inner.ScheduleRecurring(dueTime, period, func));

    public Guid ScheduleAt(DateTime runAt, Action action)
        => Track(_inner.ScheduleAt(runAt, action));

    public Guid ScheduleAt(DateTime runAt, Func<Task> func)
        => Track(_inner.ScheduleAt(runAt, func));

    public bool Pause(Guid id)
        => _myJobs.ContainsKey(id) && _inner.Pause(id);

    public bool Resume(Guid id)
        => _myJobs.ContainsKey(id) && _inner.Resume(id);

    public bool Cancel(Guid id)
    {
        if (!_myJobs.TryRemove(id, out _)) return false;
        return _inner.Cancel(id);
    }

    public void CancelAll()
    {
        foreach (var id in _myJobs.Keys)
            _inner.Cancel(id);
        _myJobs.Clear();
    }

    public IEnumerable<Guid> GetScheduledIds() 
        => _myJobs.Keys.ToList();

    private Guid Track(Guid id)
    {
        _myJobs[id] = 0;
        return id;
    }
}