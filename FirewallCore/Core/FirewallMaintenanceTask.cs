using DragonUtilities.Enums;

namespace FirewallCore.Core
{
    public class FirewallMaintenanceTask : FirewallTask
    {

        /// <summary>
        /// Called when the task is first added.
        /// </summary>
        public override void Initialize()
        {
            FirewallServiceProvider.Instance.LogAction("FirewallMaintenanceTask Initialized", LogLevel.INFO);
        }

        /// <summary>
        /// Called once before the first Tick.
        /// </summary>
        public override void StartTask()
        {
            FirewallServiceProvider.Instance.LogAction("FirewallMaintenanceTask Started", LogLevel.INFO);
        }

        /// <summary>
        /// Called on each tick.
        /// </summary>
        public override void Tick()
        {
            FirewallServiceProvider.Instance.LogAction("FirewallMaintenanceTask Tick", LogLevel.INFO);
        }

        /// <summary>
        /// Called when the task is shut down.
        /// </summary>
        public override void Shutdown()
        {
            FirewallServiceProvider.Instance.LogAction("FirewallMaintenanceTask Shutdown", LogLevel.INFO);
        }
    }
}