namespace FirewallCore.Core;

internal class SyslogMonitor
{
    public async Task StartMonitoring(CancellationToken token)
    {
        Directory.CreateDirectory(FirewallServiceProvider.LogArchiveDir);
        if (!File.Exists(FirewallServiceProvider.ConnectionLogPath))
        {
            File.Create(FirewallServiceProvider.ConnectionLogPath).Close();
        }

        using FileStream fs = new FileStream(FirewallServiceProvider.Instance.GetSysLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using StreamReader reader = new StreamReader(fs);
            
        fs.Seek(0, SeekOrigin.End);
        long lastPosition = fs.Position;
            
        while (!token.IsCancellationRequested)
        {
            fs.Seek(lastPosition, SeekOrigin.Begin);
            string line;
            bool newLineFound = false;
                
            while ((line = reader.ReadLine()) != null)
            {
                newLineFound = true;
                FirewallServiceProvider.Instance.ProcessLogLine(line);
                lastPosition = fs.Position;
            }
                
            if (newLineFound)
            {
                FirewallServiceProvider.Instance.LogManager.RotateLogs();
            }
                
            try
            {
                await Task.Delay(2000, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}