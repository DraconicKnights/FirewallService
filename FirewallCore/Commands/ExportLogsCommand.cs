using FirewallInterface.Interface;

namespace FirewallCore.Commands;

public class ExportLogsCommand : ICommand
{
    public string Name => "exportlogs";
    public string Description => "Exports the current logs to a CSV file.";
    public string Usage => "exportlogs [filename]";

    public void Execute(string[] args, IFirewallContext context, out string response)
    {
        try
        {
            // 1) Determine target filename (allow override, else timestamp + .json.enc)
            var fileName = (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
                ? args[0]
                : $"logs_{DateTime.Now:yyyyMMddHHmmss}.json.enc";

            // 2) Kick off your secure export routine
            context.ExportLogs(fileName);

            // 3) Tell the user where it landed and how to view it
            response =
                $"Encrypted logs written to '{fileName}'.\n" +
                $"To decrypt & view:  show-logs {fileName}";
        }
        catch (Exception ex)
        {
            response = $"Error exporting logs: {ex.Message}";
        }
    }

}