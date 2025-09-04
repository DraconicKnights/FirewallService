
namespace FirewallCore.Core
{
    internal class FirewallRuntimeManager
    {
        private readonly List<FirewallTask> _tasks = new();
        private CancellationTokenSource _cts;
        private readonly int _tickIntervalMilliseconds;
        private Task _runningTask;
        private bool _isRunning = false;

        /// <summary>
        /// Creates a new instance of FirewallRuntimeManager.
        /// </summary>
        /// <param name="tickIntervalMilliseconds">Interval between tick calls in milliseconds (default: 1000ms).</param>
        public FirewallRuntimeManager(int tickIntervalMilliseconds = 1000)
        {
            _tickIntervalMilliseconds = tickIntervalMilliseconds;
        }

        /// <summary>
        /// Adds a firewall task to be managed.
        /// </summary>
        public void AddTask(FirewallTask task)
        {
            _tasks.Add(task);
            task.Initialize();

            // Auto-start the runtime loop if not running already.
            if (!_isRunning)
            {
                StartAutoTick();
            }
        }

        /// <summary>
        /// Removes a firewall task from management.
        /// </summary>
        public void RemoveTask(FirewallTask task)
        {
            if (_tasks.Contains(task))
            {
                task.Shutdown();
                _tasks.Remove(task);
            }
        }

        /// <summary>
        /// Starts the execution (tick) loop.
        /// </summary>
        private void StartAutoTick()
        {
            _cts = new CancellationTokenSource();
            _isRunning = true;

            // Call StartTask on all firewall tasks.
            foreach (var task in _tasks)
            {
                task.StartTask();
            }

            _runningTask = Task.Run(async () =>
            {
                try
                {
                    while (!_cts.IsCancellationRequested)
                    {
                        foreach (var task in _tasks)
                        {
                            task.Tick();
                        }
                        await Task.Delay(_tickIntervalMilliseconds, _cts.Token);
                    }
                }
                catch (TaskCanceledException)
                {
                    // Cancellation
                }
                finally
                {
                    _isRunning = false;
                }
            }, _cts.Token);
        }

        /// <summary>
        /// Stops the tick loop.
        /// </summary>
        public void Stop()
        {
            _cts?.Cancel();
            
            //  wait for the loop to end (optional)
            try
            {
                _runningTask?.Wait();
            }
            catch
            {
               //Cancellation
            }

            //  shutdown all tasks
            foreach (var task in _tasks)
            {
                task.Shutdown();
            }

            // remove them all
            _tasks.Clear();
            _isRunning = false;
        }
    }
}