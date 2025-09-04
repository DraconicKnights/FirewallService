using DragonUtilities.Enums;
using FirewallCore.Utils;
using FirewallInterface.Interface;

namespace FirewallCore.Commands;

public class HelpCommand : ICommand
{
    public string Name => "help";
    public string Description => "Display all available commands.";
    public string Usage => "help [command]";
    public void Execute(string[] args, IFirewallContext context, out string response)
    {
        var cmds = context.CommandManager.RegisteredCommands
            .OrderBy(c => c.Name)
            .ToArray();
        
        bool isError = false;

        if (args.Length > 0)
        {
            cmds = cmds.Where(c => c.Name.Equals(args[0], StringComparison.OrdinalIgnoreCase)).ToArray();

            if (cmds.Length == 0)
            {
                isError = true;
            }
        }
        
        var header = isError ? "🔍 Command Reference Error 🔍" : "🔍 Command Reference 🔍";

        var lines = new List<string>
        {
            header,
            "",
        };
        
        if (!isError)
            lines.Add($"{"Name",-10} {"Usage",-15} Description");

        if (!isError)
        {
            var maxName = cmds.Max(c => c.Name.Length);
            var maxUsage = cmds.Max(c => c.Usage.Length);
            
            foreach (var cmd in cmds)
            {
                lines.Add(
                    cmd.Name.PadRight(maxName)
                    + "   "
                    + cmd.Usage.PadRight(maxUsage)
                    + "   "
                    + cmd.Description
                );
            }
        }
        else
        {
            lines.Add($"No command matches \"{args[0]}\".");
        }

        var box = MessageUtiliity.BuildAsciiBox(lines).Split(
            new[] { "\r\n", "\n" },
            StringSplitOptions.RemoveEmptyEntries
        );
        // choose colors
        var borderColor = isError ? ConsoleColor.Red : ConsoleColor.DarkCyan;
        var titleColor  = isError ? ConsoleColor.Yellow : ConsoleColor.Green;
        var labelColor  = ConsoleColor.Cyan;
        var textColor   = ConsoleColor.White;

        // render box with colors
        foreach (var raw in box)
        {
            if (raw.StartsWith("┌") || raw.StartsWith("└"))
            {
                Console.ForegroundColor = borderColor;
                Console.WriteLine(raw);
            }
            else if (raw.StartsWith("│"))
            {
                // extract content between the two border pipes
                var content = raw.Substring(2, raw.Length - 4);
                Console.ForegroundColor = borderColor;
                Console.Write("│ ");
                // title line
                if (content.StartsWith(header))
                {
                    Console.ForegroundColor = titleColor;
                    Console.Write(content.PadRight(raw.Length - 4));
                }
                else if (content.TrimStart().StartsWith("Name"))
                {
                    Console.ForegroundColor = labelColor;
                    Console.Write(content);
                }
                else
                {
                    Console.ForegroundColor = textColor;
                    Console.Write(content);
                }
                Console.ForegroundColor = borderColor;
                Console.WriteLine(" │");
            }
            else // safety fallback
            {
                Console.ResetColor();
                Console.WriteLine(raw);
            }
        }

        Console.ResetColor();
        response = string.Empty;
    }

}