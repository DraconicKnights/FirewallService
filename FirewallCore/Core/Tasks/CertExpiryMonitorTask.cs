using DragonUtilities.Enums;

namespace FirewallCore.Core.Tasks;

public class CertExpiryMonitorTask : FirewallTask
{
    private readonly string _certPath;
    private readonly TimeSpan _warnBefore;

    public CertExpiryMonitorTask(string certPath, TimeSpan warnBefore)
    {
        _certPath = certPath;
        _warnBefore = warnBefore;
    }

    public override void Initialize() { }
    public override void StartTask() { }

    public override void Tick()
    {
        var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(_certPath);
        var days = (cert.NotAfter - DateTime.UtcNow).TotalDays;
        if (days < _warnBefore.TotalDays)
            FirewallServiceProvider.Instance.LogAction(
                $"Certificate expires in {days:N1} days", LogLevel.WARNING);
    }

    public override void Shutdown() { }
}