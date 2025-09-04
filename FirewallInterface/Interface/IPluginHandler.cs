namespace FirewallInterface.Interface;

/// <summary>
/// Defines methods and properties to manage and access plugins.
/// </summary>
public interface IPluginHandler
{
    /// <summary>
    /// Retrieves a list of all currently enabled plugins.
    /// </summary>
    IReadOnlyList<IPlugin> Plugins { get; }

    /// <summary>
    /// Attempts to retrieve a plugin by its name.
    /// </summary>
    /// <param name="name">The name of the plugin to retrieve.</param>
    /// <param name="plugin">
    /// When this method returns, contains the plugin that matches the specified name
    /// if the plugin is found; otherwise, <c>null</c>. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <c>true</c> if a plugin with the specified name was found; otherwise, <c>false</c>.
    /// </returns>
    bool TryGetPlugin(string name, out IPlugin plugin);

    /// <summary>
    /// Tries to retrieve a plugin based on its name.
    /// </summary>
    /// <param name="name">
    /// The name of the plugin to search for.
    /// </param>
    /// <param name="plugin">
    /// When this method returns, contains the plugin with the specified name if found; otherwise, null.
    /// </param>
    /// <returns>
    /// true if a plugin with the specified name is found; otherwise, false.
    /// </returns>
    bool TryGetPlugin<T>(out T plugin) where T : class, IPlugin;
}