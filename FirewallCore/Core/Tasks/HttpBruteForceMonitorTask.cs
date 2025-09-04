using System.Collections.Concurrent;
using DragonUtilities.Enums;

namespace FirewallCore.Core.Tasks;

public class HttpBruteForceMonitorTask : FirewallTask
{
    private readonly string _accessLogPath;
    private readonly int _threshold;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, List<DateTime>> _errors = new();

    public HttpBruteForceMonitorTask(string logPath, int threshold = 50, TimeSpan? window = null)
    {
        _accessLogPath = logPath;
        _threshold = threshold;
        _window = window ?? TimeSpan.FromMinutes(1);
    }

    public override void Initialize() { }
    public override void StartTask() { }

    public override void Tick()
    {
        var now = DateTime.UtcNow;
        foreach (var line in File.ReadLines(_accessLogPath))
        {
            if (line.Contains("401") || line.Contains("404"))
            {
                var ip = ExtractIp(line);
                var list = _errors.GetOrAdd(ip, _ => new());
                list.Add(now);
                list.RemoveAll(t => now - t > _window);

                if (list.Count >= _threshold && !FirewallServiceProvider.Instance.BlockListManager.IsBlocked(ip))
                {
                    FirewallServiceProvider.Instance.ManualBlockIP(ip, 1800);
                    FirewallServiceProvider.Instance.LogAction($"Blocked {ip} for HTTP brute‐force", LogLevel.WARNING);
                    list.Clear();
                }
            }
        }
    }

    public override void Shutdown() { }

    private string ExtractIp(string line) => line.Split(' ')[0];
}