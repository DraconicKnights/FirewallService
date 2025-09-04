using FirewallInterface.Interface;

namespace FirewallCore.Commands;

public class IpCommentCommand : ICommand
{
    public string Name => "ip-comment";
    public string Description => "Adds or lists comments for an IP GUID.";
    public string Usage => "ip-comment add <ipGuid> <text> | ip-comment list <ipGuid>";

    public void Execute(string[] args, IFirewallContext ctx, out string response)
    {
        if (args.Length < 2 || 
            !Guid.TryParse(args[1], out var ipGuid) ||
            (args[0] != "add" && args[0] != "list"))
        {
            response = $"Error: invalid arguments. {Usage}";
            return;
        }

        if (args[0] == "add")
        {
            if (args.Length != 3)
            {
                response = $"Error: missing text. {Usage}";
                return;
            }
            ctx.AddComment(ipGuid, args[2]);
            response = $"Comment added to {ipGuid}.";
            return;
        }

        // list
        var comments = ctx.GetComments(ipGuid).ToList();
        if (!comments.Any())
        {
            response = $"No comments found for GUID {ipGuid}.";
            return;
        }

        response = string.Join(Environment.NewLine,
            comments.Select(c => $"[{c.Time:O}] {c.Comment}"));
    }
}