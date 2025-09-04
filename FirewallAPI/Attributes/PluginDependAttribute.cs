namespace FirewallAPI.Attributes;

/// Specifies dependencies for a plugin class, including required and optional dependencies.
/// This attribute is applied to plugin classes to define their dependency requirements.
/// Required dependencies must be present for the plugin to function, while optional
/// dependencies provide additional functionality if available.
[AttributeUsage(AttributeTargets.Class)]
public class PluginDependAttribute : Attribute
{
    /// <summary>
    /// Represents the names of plugins that are required for a class to be loaded and initialized.
    /// </summary>
    public string[] RequiredDependencies { get; }

    /// <summary>
    /// Plugin names that are optional for functionality but may enhance performance or features if present.
    /// </summary>
    public string[] OptionalDependencies { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Specifies the dependencies required for a plugin to function. This attribute can be applied to classes representing plugins.
    /// </summary>
    /// <param name="requiredDependencies">Names of plugins that are required for the annotated plugin to function properly.</param>
    public PluginDependAttribute(params string[] requiredDependencies)
    {
        RequiredDependencies = requiredDependencies ?? Array.Empty<string>();
    }
}