namespace FirewallInterface.Interface;

/// <summary>
/// Defines an interface for scheduling tasks, supporting both one-time and recurring execution, with capabilities to manage, pause, resume, and cancel scheduled tasks.
/// </summary>
public interface ISchedulerService
{
    /// <summary>
    /// Schedules a one-time action to be executed after the specified delay.
    /// </summary>
    /// <param name="delay">The amount of time to wait before executing the action.</param>
    /// <param name="action">The action to be executed after the delay.</param>
    /// <returns>A GUID that uniquely identifies the scheduled action, which can be used for cancellation.</returns>
    Guid ScheduleOnce(TimeSpan delay, Action action);

    /// <summary>
    /// Schedules a one-time asynchronous function to be executed after the specified delay.
    /// </summary>
    /// <param name="delay">The duration to wait before executing the function.</param>
    /// <param name="func">The asynchronous function to execute after the delay.</param>
    /// <returns>A unique identifier (GUID) that represents the scheduled task and can be used for cancellation.</returns>
    Guid ScheduleOnce(TimeSpan delay, Func<Task> func);

    // Schedules a one-time stateful action.
    /// <summary>
    /// Schedules a one-time stateful action to be executed after the specified delay.
    /// </summary>
    /// <param name="delay">The amount of time to wait before executing the action.</param>
    /// <param name="action">The action to be executed, which accepts a state parameter of type <typeparamref name="TState"/>.</param>
    /// <param name="state">The state object of type <typeparamref name="TState"/> to be passed to the action when it is executed.</param>
    /// <returns>A GUID that uniquely identifies the scheduled action, which can be used for cancellation.</returns>
    Guid ScheduleOnce<TState>(TimeSpan delay, Action<TState> action, TState state);

    /// <summary>
    /// Schedules a one-time, stateful asynchronous task to be executed after the specified delay.
    /// </summary>
    /// <param name="delay">The amount of time to wait before executing the task.</param>
    /// <param name="func">The asynchronous function to execute, which accepts a state object as a parameter.</param>
    /// <param name="state">The state object to pass to the asynchronous function at execution time.</param>
    /// <returns>A GUID that uniquely identifies the scheduled task, which can be used to cancel it.</returns>
    Guid ScheduleOnce<TState>(TimeSpan delay, Func<TState, Task> func, TState state);

    /// <summary>
    /// Schedules a recurring action to be executed at the specified intervals.
    /// </summary>
    /// <param name="dueTime">The amount of time to delay before the first execution of the action.</param>
    /// <param name="period">The time interval between subsequent executions of the action.</param>
    /// <param name="action">The action to be executed at each interval.</param>
    /// <returns>A GUID that uniquely identifies the scheduled recurring action and can be used to manage or cancel it.</returns>
    Guid ScheduleRecurring(TimeSpan dueTime, TimeSpan period, Action action);

    /// <summary>
    /// Schedules a recurring asynchronous function to be executed at a specified interval.
    /// </summary>
    /// <param name="dueTime">The amount of time to delay before the first execution of the function.</param>
    /// <param name="period">The interval between subsequent executions of the function.</param>
    /// <param name="func">The asynchronous function to execute on a recurring basis.</param>
    /// <returns>A GUID that uniquely identifies the scheduled recurring function, which can be used for cancellation.</returns>
    Guid ScheduleRecurring(TimeSpan dueTime, TimeSpan period, Func<Task> func);

    // Schedules an action at an exact DateTime.
    /// <summary>
    /// Schedules an action to be executed at the specified exact date and time.
    /// </summary>
    /// <param name="runAt">The exact date and time at which the action should be executed.</param>
    /// <param name="action">The action to be executed at the scheduled time.</param>
    /// <returns>A GUID that uniquely identifies the scheduled action, which can be used for cancellation.</returns>
    Guid ScheduleAt(DateTime runAt, Action action);

    /// <summary>
    /// Schedules a one-time task to be executed at the specified date and time.
    /// </summary>
    /// <param name="runAt">The exact date and time when the task should be executed.</param>
    /// <param name="func">The asynchronous action to be executed at the specified time.</param>
    /// <returns>A unique identifier (GUID) for the scheduled task, which can be used to manage or cancel it.</returns>
    Guid ScheduleAt(DateTime runAt, Func<Task> func);

    /// <summary>
    /// Pauses a scheduled task, preventing it from being executed until resumed.
    /// </summary>
    /// <param name="id">The unique identifier (GUID) of the scheduled task to pause.</param>
    /// <returns>True if the task was successfully paused; otherwise, false.</returns>
    bool Pause(Guid id);

    /// <summary>
    /// Resumes a paused scheduled task identified by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the task to resume.</param>
    /// <returns>True if the task was successfully resumed; otherwise, false.</returns>
    bool Resume(Guid id);

    /// <summary>
    /// Cancels a scheduled task identified by the given GUID.
    /// </summary>
    /// <param name="id">The unique identifier of the scheduled task, returned by ScheduleOnce or ScheduleRecurring methods.</param>
    /// <returns>True if the task was successfully found and cancelled; otherwise, false.</returns>
    bool Cancel(Guid id);

    /// <summary>
    /// Cancels all scheduled tasks and clears the internal scheduler.
    /// Any recurring or one-time tasks previously scheduled will be stopped and removed.
    /// </summary>
    void CancelAll();

    /// <summary>
    /// Retrieves a collection of unique identifiers (GUIDs) for all currently scheduled tasks.
    /// </summary>
    /// <returns>An enumerable collection of GUIDs representing scheduled tasks.</returns>
    IEnumerable<Guid> GetScheduledIds();
}