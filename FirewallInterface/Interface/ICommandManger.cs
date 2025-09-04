namespace FirewallInterface.Interface;

/// <summary>
/// Provides functionality to manage, retrieve, and execute commands for a firewall context.
/// </summary>
public interface ICommandManager
{
    /// <summary>
    /// Retrieves a collection of all commands currently registered with the command manager.
    /// </summary>
    IEnumerable<ICommand> RegisteredCommands { get; }

    /// <summary>
    /// Retrieves a command based on its concrete type.
    /// </summary>
    /// <typeparam name="T">The type of the command to be retrieved.</typeparam>
    /// <param name="command">The output parameter that will hold the retrieved command. It will be null if the command is not found.</param>
    void GetCommand<T>(out ICommand? command)
        where T : ICommand;

    /// <summary>
    /// Retrieves a command by its name (case-insensitive).
    /// </summary>
    /// <param name="name">The name of the command to retrieve.</param>
    /// <param name="command">The retrieved command, or null if not found.</param>
    void GetCommand(string name, out ICommand? command);

    /// <summary>
    /// Processes a raw command input string and executes it within the provided firewall context.
    /// </summary>
    /// <param name="input">The raw command string to be parsed and executed.</param>
    /// <param name="ctx">The context within which the command will be executed.</param>
    /// <param name="response">The output response generated as a result of command execution.</param>
    void ProcessCommand(string input, IFirewallContext ctx, out string response);

    /// <summary>
    /// Processes and executes a specific command of the given type with the specified arguments and firewall context.
    /// </summary>
    /// <param name="args">The arguments to be passed to the command for execution.</param>
    /// <param name="context">The firewall context in which the command will be executed.</param>
    /// <param name="response">The response generated after executing the command.</param>
    /// <typeparam name="TCommand">The type of the command to be processed, which implements <see cref="ICommand"/>.</typeparam>
    void ProcessCommand<TCommand>(string args, IFirewallContext context, out string response) where TCommand : ICommand;
}
