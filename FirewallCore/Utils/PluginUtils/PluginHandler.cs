using FirewallInterface.Interface;

namespace FirewallCore.Core;

/// <summary>
/// Handles operations related to plugins in the system by acting as an abstraction layer
/// over the PluginManager. Provides methods for retrieving plugins by name, type, or ID.
/// </summary>
public class PluginHandler : IPluginHandler
{
    /// <summary>
    /// Provides a read-only collection of all plugins that are currently registered and managed
    /// by the Plugin Manager. Each plugin implements the <see cref="IPlugin"/> interface.
    /// </summary>
    /// <remarks>
    /// This property is commonly used to retrieve and inspect the list of loaded plugins,
    /// allowing interaction with their respective properties and capabilities.
    /// </remarks>
    public IReadOnlyList<IPlugin> Plugins => FirewallServiceProvider.Instance.PluginManager.Plugins;

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
    public bool TryGetPlugin(string name, out IPlugin plugin) => FirewallServiceProvider.Instance.PluginManager.TryGetPlugin(name, out plugin);

    /// Tries to retrieve a plugin of the specified type from the plugin manager.
    /// If a plugin of the specified type is found, it is returned through the output parameter.
    /// <typeparam name="T">
    /// The type of the plugin to retrieve, which must be a class implementing the <see cref="IPlugin"/> interface.
    /// </typeparam>
    /// <param name="plugin">
    /// When this method returns, contains the plugin of the specified type if found; otherwise, null.
    /// </param>
    /// <returns>
    /// true if a plugin of the specified type is found; otherwise, false.
    /// </returns>
    public bool TryGetPlugin<T>(out T plugin) where T : class, IPlugin => FirewallServiceProvider.Instance.PluginManager.TryGetPlugin<T>(out plugin);

    /// <summary>
    /// Retrieves a plugin by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the plugin to retrieve.</param>
    /// <returns>The <see cref="IPlugin"/> associated with the provided identifier.</returns>
    public IPlugin GetPlugin(Guid id) => FirewallServiceProvider.Instance.PluginManager.GetPlugin(id);
}