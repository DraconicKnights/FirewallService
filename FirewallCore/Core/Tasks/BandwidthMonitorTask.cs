using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using DragonUtilities.Enums;
using FirewallEvent.Events.Core;
using FirewallEvent.Events.EventCalls;

namespace FirewallCore.Core.Tasks;

public class BandwidthMonitorTask : FirewallTask
{
        private readonly string _interfaceName;
        private long _lastBytesRecv, _lastBytesSent;
        private readonly long _thresholdBytesPerSec;

        public BandwidthMonitorTask(string interfaceName, long thresholdBps)
        {
            _interfaceName = interfaceName;
            _thresholdBytesPerSec = thresholdBps;
        }

        public override void Initialize()
        {
            (_lastBytesRecv, _lastBytesSent) = ReadCounters();
        }

        public override void StartTask() { }

        public override void Tick()
        {
            var (recv, sent) = ReadCounters();
            var delta = (recv - _lastBytesRecv) + (sent - _lastBytesSent);
            if (delta > _thresholdBytesPerSec)
            {
                FirewallEventService.Instance.Publish(new BandwidthExceededEvent(_interfaceName, delta, _thresholdBytesPerSec));
                
                FirewallServiceProvider.Instance.LogAction(
                    $"High bandwidth on {_interfaceName}: {delta} B/s",
                    LogLevel.WARNING);
            }

            _lastBytesRecv = recv;
            _lastBytesSent = sent;
        }

        public override void Shutdown() { }

        private (long recv, long sent) ReadCounters()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var lines = File.ReadAllLines("/proc/net/dev");
                var line = lines
                    .Select(l => l.Trim())
                    .FirstOrDefault(l => l.StartsWith(_interfaceName + ":", StringComparison.Ordinal));
                if (line != null)
                {
                    var parts = line.Split(new[] { ':', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    // parts[1] = bytes received, parts[9] = bytes transmitted
                    if (long.TryParse(parts[1], out var r) && long.TryParse(parts[9], out var s))
                        return (r, s);
                }
                return (0, 0);
            }
            else
            {
                // Windows / cross-platform using NetworkInterface
                var ni = NetworkInterface
                    .GetAllNetworkInterfaces()
                    .FirstOrDefault(n =>
                        string.Equals(n.Name, _interfaceName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(n.Description, _interfaceName, StringComparison.OrdinalIgnoreCase));
                if (ni != null)
                {
                    var stats = ni.GetIPv4Statistics();
                    return (stats.BytesReceived, stats.BytesSent);
                }
                return (0, 0);
            }
        }
}