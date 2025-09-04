namespace FirewallInterface.Interface;

/// <summary>
/// Defines methods for managing plugin configurations, including saving, loading,
/// checking existence, deleting, listing, and creating backups of configuration files
/// for plugins.
/// </summary>
public interface IPluginConfigManager
{
    /// <summary>
    /// Loads the configuration of a specified type for the given plugin and configuration name.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the configuration object to load. Must have a parameterless constructor.
    /// </typeparam>
    /// <param name="pluginName">
    /// The name of the plugin for which the configuration is being loaded.
    /// </param>
    /// <param name="configName">
    /// The name of the configuration file to load.
    /// </param>
    /// <returns>
    /// The loaded configuration object of type <typeparamref name="T"/>.
    /// </returns>
    T LoadConfig<T>(string pluginName, string configName)
        where T : new();

    /// <summary>
    /// Saves the specified configuration object to a designated file within the plugin's directory structure.
    /// </summary>
    /// <param name="pluginName">The name of the plugin associated with the configuration.</param>
    /// <param name="configName">The name of the configuration file to save.</param>
    /// <param name="config">The configuration object to be saved.</param>
    /// <typeparam name="T">The type of the configuration object.</typeparam>
    void SaveConfig<T>(string pluginName, string configName, T config)
        where T : new();

    /// <summary>
    /// Determines whether a configuration file exists for the specified plugin and configuration name.
    /// </summary>
    /// <param name="plugin">The name of the plugin for which to check the configuration file.</param>
    /// <param name="name">The name of the configuration file to check.</param>
    /// <returns>True if the configuration file exists; otherwise, false.</returns>
    bool ConfigExists(string plugin, string name);

    /// <summary>
    /// Deletes the specified configuration file for a given plugin.
    /// </summary>
    /// <param name="plugin">The name of the plugin associated with the configuration.</param>
    /// <param name="name">The name of the configuration file to be deleted.</param>
    void DeleteConfig(string plugin, string name);

    /// <summary>
    /// Retrieves a collection of configuration file names for a specified plugin.
    /// </summary>
    /// <param name="plugin">The name of the plugin whose configuration file names are to be listed.</param>
    /// <returns>An enumerable collection of configuration file names, excluding their file extensions.</returns>
    IEnumerable<string> ListConfigs(string plugin);

    /// Loads a configuration with the specified plugin and name. If it does not exist,
    /// uses the provided fallback factory to create a default configuration, saves it,
    /// and returns the created default configuration.
    /// <param name="plugin">The name of the plugin for which the configuration is managed.</param>
    /// <param name="name">The specific name of the configuration within the plugin.</param>
    /// <param name="fallback">A factory function to generate the default configuration if it does not exist.</param>
    /// <typeparam name="T">The type of the configuration object. It must have a parameterless constructor.</typeparam>
    /// <returns>The loaded configuration if it exists; otherwise, the default configuration created by the fallback factory.</returns>
    T LoadOrDefault<T>(string plugin, string name, Func<T> fallback) where T : new();

    /// <summary>
    /// Backs up a specific plugin configuration file to a designated backup directory.
    /// </summary>
    /// <param name="plugin">The name of the plugin associated with the configuration file.</param>
    /// <param name="name">The name of the configuration file to back up, without the file extension.</param>
    /// <param name="backupDir">The directory where the backup will be stored.</param>
    void BackupConfig(string plugin, string name, string backupDir);

    /// Creates a backup of all configuration files for a specified plugin.
    /// Copies all configuration files belonging to the specified plugin
    /// to the provided backup directory.
    /// <param name="plugin">
    /// The name of the plugin whose configuration files are to be backed up.
    /// This name serves as the identifier for the plugin directory containing the configurations.
    /// </param>
    /// <param name="backupDir">
    /// The destination directory where the backup of the configuration files will be saved.
    /// This directory will be created if it does not exist.
    /// </param>
    void BackupAllConfigs(string plugin, string backupDir);
}