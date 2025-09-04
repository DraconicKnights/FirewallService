using System.Collections.Concurrent;
using FirewallInterface.Interface;

namespace FirewallCore.Utils
{
    /// <summary>
    /// Provides scheduling capabilities for executing one-time or recurring tasks.
    /// Implements IDisposable and ISchedulerService.
    /// </summary>
    public sealed class SchedulerService : IDisposable, ISchedulerService
    {
        private readonly ConcurrentDictionary<Guid, Timer> _timers = new();
        private readonly ConcurrentDictionary<Guid, Job> _jobs = new();

        /// <summary>
        /// Schedule a one-time action after the given delay.
        /// </summary>
        /// <param name="delay">Time to wait before invoking action.</param>
        /// <param name="action">The callback to invoke.</param>
        /// <returns>A GUID identifier which can be used to cancel.</returns>
        public Guid ScheduleOnce(TimeSpan delay, Action action)
        {
            if (action is null) throw new ArgumentNullException(nameof(action));
            return ScheduleInternal(delay, Timeout.InfiniteTimeSpan, state => action());
        }

        /// <summary>
        /// Schedule a one-time async function after the given delay.
        /// </summary>
        public Guid ScheduleOnce(TimeSpan delay, Func<Task> func)
        {
            if (func is null) throw new ArgumentNullException(nameof(func));
            return ScheduleInternal(delay, Timeout.InfiniteTimeSpan, async state => await SafeInvokeAsync(func));
        }
        
        public Guid ScheduleOnce<TState>(TimeSpan delay, Action<TState> action, TState state)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return ScheduleInternal(delay, Timeout.InfiniteTimeSpan,
                _ => action(state));
        }

        public Guid ScheduleOnce<TState>(TimeSpan delay, Func<TState, Task> func, TState state)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            return ScheduleInternal(delay, Timeout.InfiniteTimeSpan,
                async _ => await SafeInvokeAsync(() => func(state)));
        }

        /// <summary>
        /// Schedule a recurring action.
        /// </summary>
        /// <param name="dueTime">Delay before first run.</param>
        /// <param name="period">Interval between runs.</param>
        /// <param name="action">The callback to invoke.</param>
        /// <returns>A GUID identifier which can be used to cancel.</returns>
        public Guid ScheduleRecurring(TimeSpan dueTime, TimeSpan period, Action action)
        {
            if (action is null) throw new ArgumentNullException(nameof(action));
            return ScheduleInternal(dueTime, period, state => action());
        }

        /// <summary>
        /// Schedule a recurring async function.
        /// </summary>
        public Guid ScheduleRecurring(TimeSpan dueTime, TimeSpan period, Func<Task> func)
        {
            if (func is null) throw new ArgumentNullException(nameof(func));
            return ScheduleInternal(dueTime, period, async state => await SafeInvokeAsync(func));
        }
        
        public Guid ScheduleAt(DateTime runAt, Action action)
        {
            var delay = runAt - DateTime.UtcNow;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
            return ScheduleOnce(delay, action);
        }

        public Guid ScheduleAt(DateTime runAt, Func<Task> func)
        {
            var delay = runAt - DateTime.UtcNow;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
            return ScheduleOnce(delay, func);
        }
        
        public bool Pause(Guid id)
        {
            if (_jobs.TryGetValue(id, out var job) && !job.IsPaused)
            {
                job.Timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                job.IsPaused = true;
                return true;
            }
            return false;
        }

        public bool Resume(Guid id)
        {
            if (_jobs.TryGetValue(id, out var job) && job.IsPaused)
            {
                job.Timer.Change(job.DueTime, job.Period);
                job.IsPaused = false;
                return true;
            }
            return false;
        }



        /// <summary>
        /// Cancel a scheduled task.
        /// </summary>
        /// <param name="id">The identifier returned from ScheduleOnce or ScheduleRecurring.</param>
        /// <returns>True if a timer was found and cancelled.</returns>
        public bool Cancel(Guid id)
        {
            if (_timers.TryRemove(id, out var timer))
            {
                timer.Dispose();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Cancel all scheduled tasks.
        /// </summary>
        public void CancelAll()
        {
            foreach (var kv in _timers)
            {
                kv.Value.Dispose();
            }
            _timers.Clear();
        }

        public void Dispose()
        {
            CancelAll();
        }
        
        public IEnumerable<Guid> GetScheduledIds()
        {
            return _jobs.Keys.ToList();
        }

        private Guid ScheduleInternal(TimeSpan dueTime, TimeSpan period, TimerCallback callback)
        {
            var id = Guid.NewGuid();
            Timer? timer = null;

            void Wrapped(object? state)
            {
                try
                {
                    callback(state);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Scheduler:{id}] exception: {ex}");
                }

                if (period == Timeout.InfiniteTimeSpan)
                    Cancel(id);
            }

            timer = new Timer(state => Wrapped(state), null, dueTime, period);
            _timers[id] = timer;
            return id;
        }

        private async Task SafeInvokeAsync(Func<Task> func)
        {
            try
            {
                await func();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Scheduler:async] exception: {ex}");
            }
        }
        
        private class Job
        {
            public Timer Timer;
            public TimeSpan DueTime, Period;
            public bool IsPaused;
        }
    }
}