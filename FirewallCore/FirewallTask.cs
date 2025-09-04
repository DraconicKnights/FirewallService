namespace FirewallCore;

public abstract class FirewallTask
{
    public FirewallTask()
    {
        FirewallServiceProvider.Instance.RuntimeManager.AddTask(this);
    }
    
    /// <summary>
    /// Called when the task is added to the runtime manager.
    /// </summary>
    public virtual void Initialize() { }

    /// <summary>
    /// Called once before the first Tick call.
    /// </summary>
    public virtual void StartTask() { }

    /// <summary>
    /// Called on each update cycle.
    /// </summary>
    public virtual void Tick() { }

    /// <summary>
    /// Called when the task is removed or the runtime is stopping.
    /// </summary>
    public virtual void Shutdown() { }
}