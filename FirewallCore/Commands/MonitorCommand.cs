using FirewallCore.Utils;
using FirewallInterface.Interface;

namespace FirewallCore.Commands;

public class MonitorCommand : ICommand
{
    public string Name => "monitor";
    public string Description => "Enter real-time monitoring mode (press 'q' to quit).";
    public string Usage => "monitor";

    public void Execute(string[] args, IFirewallContext context, out string response)
    {
        Console.TreatControlCAsInput = true;
        var monitor = new MonitoringService(context, FirewallServiceProvider.Instance.SchedulerService);
        monitor.Start(); 

        // Run monitor service until user presses 'q'
        while (true)
        {
            if (Console.KeyAvailable && Console.ReadKey(intercept: true).Key == ConsoleKey.Q)
            {
                break;
            }
            Thread.Sleep(100);
        }

        monitor.Stop();
        Console.TreatControlCAsInput = false;
        response = "Exited monitor mode.";
    }
}
