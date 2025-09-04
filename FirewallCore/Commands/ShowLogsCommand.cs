using System;
using System.Linq;
using System.Collections.Generic;
using FirewallInterface.Interface;

namespace FirewallCore.Commands
{
    public class ShowLogsCommand : ICommand
    {
        public string Name => "show-logs";
        public string Description => "Decrypts and displays a previously exported log file with navigation, search, and coloring.";
        public string Usage => "show-logs [filename]";

        public void Execute(string[] args, IFirewallContext context, out string response)
        {
            response = null;
            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                response = $"Usage: {Usage}";
                return;
            }

            string decrypted;
            try
            {
                decrypted = context.ReadExportedLogs(args[0].Trim());
            }
            catch (Exception ex)
            {
                response = $"Error reading '{args[0]}': {ex.Message}";
                return;
            }

            var lines = decrypted.Replace("\r", "").Split('\n');
            int pageHeight = Math.Max(3, Console.WindowHeight - 2);
            int top = 0;
            bool quit = false;

            string searchTerm = null;
            List<int> matches = new();
            int matchCursor = -1;

            while (!quit)
            {
                Console.Clear();
                // draw window
                for (int i = 0; i < pageHeight; i++)
                {
                    int idx = top + i;
                    if (idx >= lines.Length) break;
                    var text = lines[idx];
                    // highlight entire line if it's current match
                    bool isMatchLine = searchTerm != null && idx == (matchCursor >= 0 ? matches[matchCursor] : -1);

                    if (isMatchLine)
                    {
                        Console.BackgroundColor = ConsoleColor.DarkGray;
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    var fields = text.Split('|').Select(f => f.Trim()).ToArray();
                    for (int j = 0; j < fields.Length; j++)
                    {
                        var part = fields[j];
                        var fieldColor = ConsoleColor.Gray;
                        // determine base field color
                        if (DateTime.TryParse(part, out _)) fieldColor = ConsoleColor.Green;
                        else if (part.StartsWith("ID=")) fieldColor = ConsoleColor.Cyan;
                        else if (part.StartsWith("PID=") || part.StartsWith("TID=")) fieldColor = ConsoleColor.Yellow;
                        else if (part.StartsWith("IP=")) fieldColor = ConsoleColor.Magenta;
                        else if (part.StartsWith("Host=")) fieldColor = ConsoleColor.White;
                        else if (part.StartsWith("Country=")) fieldColor = ConsoleColor.Blue;
                        else if (part.StartsWith("SrcPort=") || part.StartsWith("DstPort=")) fieldColor = ConsoleColor.DarkCyan;
                        else if (part.StartsWith("Attempts=")) fieldColor = ConsoleColor.Red;
                        else if (part.StartsWith("Window=")) fieldColor = ConsoleColor.DarkRed;

                        // print with search-term highlight
                        int p = 0;
                        Console.ForegroundColor = fieldColor;
                        while (true)
                        {
                            int idxT = searchTerm != null
                                ? part.IndexOf(searchTerm, p, StringComparison.OrdinalIgnoreCase)
                                : -1;
                            if (idxT < 0)
                            {
                                Console.Write(part[p..]);
                                break;
                            }
                            Console.Write(part[p..idxT]);
                            // highlight match
                            Console.BackgroundColor = ConsoleColor.Yellow;
                            Console.ForegroundColor = ConsoleColor.Black;
                            Console.Write(part.Substring(idxT, searchTerm.Length));
                            // restore field
                            Console.BackgroundColor = isMatchLine ? ConsoleColor.DarkGray : ConsoleColor.Black;
                            Console.ForegroundColor = fieldColor;
                            p = idxT + searchTerm.Length;
                        }

                        Console.ResetColor();
                        if (isMatchLine)
                        {
                            Console.BackgroundColor = ConsoleColor.DarkGray;
                        }

                        if (j < fields.Length - 1)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.Write(" | ");
                            Console.ResetColor();
                            if (isMatchLine) Console.BackgroundColor = ConsoleColor.DarkGray;
                        }
                    }
                    Console.ResetColor();
                    Console.WriteLine();
                }

                // footer
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine(
                    $"Lines {top + 1}-{Math.Min(top + pageHeight, lines.Length)} of {lines.Length}    " +
                    "↑/↓ scroll  PgUp/PgDn page  Home/End  T=search  n/N next/prev  Q=quit"
                );
                Console.ResetColor();

                var key = Console.ReadKey(true).Key;
                switch (key)
                {
                    case ConsoleKey.DownArrow:
                        if (top + pageHeight < lines.Length) top++;
                        break;
                    case ConsoleKey.UpArrow:
                        if (top > 0) top--;
                        break;
                    case ConsoleKey.PageDown:
                        top = Math.Min(top + pageHeight, lines.Length - pageHeight);
                        break;
                    case ConsoleKey.PageUp:
                        top = Math.Max(top - pageHeight, 0);
                        break;
                    case ConsoleKey.Home:
                        top = 0;
                        break;
                    case ConsoleKey.End:
                        top = Math.Max(lines.Length - pageHeight, 0);
                        break;
                    case ConsoleKey.T: // '/'
                        Console.ResetColor();
                        Console.ForegroundColor = ConsoleColor.DarkCyan;
                        Console.Write("Search: ");
                        Console.ResetColor();
                        searchTerm = Console.ReadLine();
                        matches = string.IsNullOrEmpty(searchTerm)
                            ? new List<int>()
                            : lines
                                .Select((l, i) => (l, i))
                                .Where(t => t.l.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                                .Select(t => t.i)
                                .ToList();
                        matchCursor = matches.Count > 0 ? 0 : -1;
                        if (matchCursor >= 0)
                            top = matches[matchCursor];
                        break;
                    case ConsoleKey.N:
                        if (matches.Count > 0)
                        {
                            matchCursor = (matchCursor + 1) % matches.Count;
                            top = matches[matchCursor];
                        }
                        break;
                    case ConsoleKey.B:
                        if (matches.Count > 0)
                        {
                            matchCursor = (matchCursor - 1 + matches.Count) % matches.Count;
                            top = matches[matchCursor];
                        }
                        break;
                    case ConsoleKey.Q:
                        quit = true;
                        break;
                }
            }

            response = string.Empty;
        }
    }
}