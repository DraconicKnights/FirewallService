using System.Text;

namespace FirewallCore.Utils;

public static class MessageUtiliity
{
    public static string BuildAsciiBox(IEnumerable<string> lines, int minWidth = 74)
    {
        var content = lines.ToList();
        var width = Math.Max(content.Max(l => l.Length) + 4, minWidth);
        var horizontal = new string('─', width - 2);
        var sb = new StringBuilder();

        sb.AppendLine($"┌{horizontal}┐");
        foreach (var line in content)
        {
            sb.AppendLine($"│ {line.PadRight(width - 4)} │");
        }
        sb.AppendLine($"└{horizontal}┘");

        return sb.ToString();
    }
}