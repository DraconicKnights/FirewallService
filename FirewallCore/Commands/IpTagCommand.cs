using DragonUtilities.Enums;
using FirewallInterface.Interface;

namespace FirewallCore.Commands;

public class IpTagCommand : ICommand
{
    public string Name => "ip-tag";
    public string Description => "Add, remove or list tags on an IP GUID.";
    public string Usage => "ip-tag <add|remove|list> <ipGuid> [tag]";

    public void Execute(string[] args, IFirewallContext ctx, out string response)
    {
        if (args.Length < 2
            || !(args[0] == "add" || args[0] == "remove" || args[0] == "list")
            || !Guid.TryParse(args[1], out var guid))
        {
            response = $"ERROR: invalid args. {Usage}";
            return;
        }

        var action = args[0];
        try
        {
            switch (action)
            {
                case "add":
                    if (args.Length != 3) { goto default; }
                    ctx.AddTag(guid, args[2]);
                    response = $"Tag '{args[2]}' added to {guid}.";
                    break;

                case "remove":
                    if (args.Length != 3) { goto default; }
                    ctx.RemoveTag(guid, args[2]);
                    response = $"Tag '{args[2]}' removed from {guid}.";
                    break;

                case "list":
                    var tags = ctx.GetTags(guid);
                    response = tags.Any()
                        ? string.Join(", ", tags)
                        : "(no tags)";
                    break;

                default:
                    response = $"ERROR: invalid args. {Usage}";
                    break;
            }
        }
        catch (Exception ex)
        {
            ctx.LogAction($"[ip-tag] {ex.Message}", LogLevel.ERROR);
            response = "ERROR: tag operation failed.";
        }
    }

}