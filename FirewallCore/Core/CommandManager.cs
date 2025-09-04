using DragonUtilities.Enums;
using FirewallCore.Commands;
using FirewallInterface.Interface;

namespace FirewallCore.Core;

internal class CommandManager : ICommandManager
{
    public Dictionary<string, ICommand> Commands { get; } = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<ICommand> RegisteredCommands => Commands.Values;

    public CommandManager()
    {
        // Register available commands.
        RegisterCommand(new ListCommand());
        RegisterCommand(new BlockCommand());
        RegisterCommand(new UnblockCommand());
        RegisterCommand(new StatusCommand());
        RegisterCommand(new RotateCommand());
        RegisterCommand(new ReloadCommand());
        RegisterCommand(new ClearCommand());
        RegisterCommand(new WhitelistCommand());
        RegisterCommand(new UnblockAllCommand());
        RegisterCommand(new ExportLogsCommand());
        RegisterCommand(new ShowLogsCommand());
        RegisterCommand(new InfoCommand());
        RegisterCommand(new HelpCommand());
        RegisterCommand(new ExitCommand());
        RegisterCommand(new IpHistoryCommand());
        RegisterCommand(new IpCommentCommand());
        RegisterCommand(new IpTagCommand());
        RegisterCommand(new MonitorCommand());

    }
    
    public void RegisterCommand(ICommand command)
    {
        if (!Commands.ContainsKey(command.Name))
        {
            Commands.Add(command.Name, command);
        }
    }

    public void UnregisterCommand(ICommand command)
    {
        if (Commands.ContainsKey(command.Name))
        {
            Commands.Remove(command.Name);
        }
    }

    public void ProcessCommand(string input, IFirewallContext context, out string response)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            response = string.Empty;
            return;
        }

        // Split the command into parts.
        string[] parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string mainCommand = parts[0];
        string[] args = parts.Skip(1).ToArray();

        if (Commands.TryGetValue(mainCommand, out ICommand command))
        {
            command.Execute(args, context, out var responseText);
            response = responseText;
        }
        else
        {
            context.LogAction("Unknown command. Type 'help' for available commands.", LogLevel.INFO);
            response = string.Empty;
        }
    }

    public void ProcessCommand<TCommand>(string args ,IFirewallContext context, out string response) where TCommand : ICommand
    {
        GetCommand<TCommand>(out var command);

        if (command == null)
        {
            context.LogAction($"Command: '{typeof(TCommand).Name}' not found.", LogLevel.ERROR);
            response = string.Empty;
            return;
        }
        
        ProcessCommand(args, context, out response);
    }

    public void GetCommand(string commandName, out ICommand command)
    {
        command = Commands[commandName];
    }
    
    public void GetCommand<T>(out ICommand command) where T : ICommand
    {
        command = Commands.Values.FirstOrDefault(ic => ic is T)!;
    }
    
}