using System.IO.Compression;
using DragonUtilities.Controller;
using DragonUtilities.Enums;

namespace FirewallCore.Core;

internal class LogManager : BaseController
{
    public void AppendLog(string logEntry)
    {
        File.AppendAllText(FirewallServiceProvider.ConnectionLogPath, logEntry + Environment.NewLine);
    }
        
    public void RotateLogs()
    {
        int lineCount = File.Exists(FirewallServiceProvider.ConnectionLogPath)
            ? File.ReadAllLines(FirewallServiceProvider.ConnectionLogPath).Length
            : 0;
        if (lineCount >= FirewallServiceProvider.LogRotationLineCount)
        {
            string archiveName = Path.Combine(FirewallServiceProvider.LogArchiveDir, $"connection_attempts_{DateTime.Now:yyyyMMddHHmmss}.txt");
            Directory.CreateDirectory(FirewallServiceProvider.LogArchiveDir);
            File.Move(FirewallServiceProvider.ConnectionLogPath, archiveName);

            using (FileStream originalFileStream = new FileStream(archiveName, FileMode.Open))
            using (FileStream compressedFileStream = File.Create(archiveName + ".gz"))
            using (GZipStream compressionStream = new GZipStream(compressedFileStream, CompressionMode.Compress))
            {
                originalFileStream.CopyTo(compressionStream);
            }
            File.Delete(archiveName);
            Logger.Log("Log file rotated and compressed.", LogLevel.INFO);

            // Remove older archives if the limit is exceeded.
            var archives = new DirectoryInfo(FirewallServiceProvider.LogArchiveDir)
                .GetFiles("connection_attempts_*.txt.gz")
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();
            if (archives.Count > FirewallServiceProvider.MaxLogArchives)
            {
                foreach (var file in archives.Skip(FirewallServiceProvider.MaxLogArchives))
                {
                    file.Delete();
                    Logger.Log($"Removed old archive: {file.Name}", LogLevel.DEBUG);
                }
            }
        }
    }
    
    /// <summary>
    /// Reads all current and archived logs, returning each line.
    /// </summary>
    public IEnumerable<string> GetAllLogs()
    {
        var allLines = new List<string>();

        //  current connection log
        if (File.Exists(FirewallServiceProvider.ConnectionLogPath))
        {
            allLines.AddRange(File.ReadAllLines(FirewallServiceProvider.ConnectionLogPath));
        }

        //  any .gz archives
        var dir = new DirectoryInfo(FirewallServiceProvider.LogArchiveDir);
        if (dir.Exists)
        {
            foreach (var gz in dir.GetFiles("*.gz").OrderBy(f => f.Name))
            {
                using var fs = gz.OpenRead();
                using var gzStream = new GZipStream(fs, CompressionMode.Decompress);
                using var reader = new StreamReader(gzStream);
                while (!reader.EndOfStream)
                {
                    allLines.Add(reader.ReadLine());
                }
            }
        }

        return allLines;
    }
}