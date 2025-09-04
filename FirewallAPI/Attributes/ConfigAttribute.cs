namespace FirewallAPI.Attributes;

/// <summary>
/// Represents an attribute used to mark fields or properties within a plugin that
/// should be automatically wired up with configuration objects.
/// </summary>
/// <remarks>
/// This attribute can be applied to public or private fields and properties within
/// a class that derives from <see cref="PluginBase"/>. The specified field or property
/// will be automatically initialized with the configuration object corresponding to
/// the plugin by the <see cref="PluginBase"/> class during plugin initialization.
/// </remarks>
/// <example>
/// Applying this attribute to a field or property will allow the <see cref="IPluginConfigManager"/>
/// to automatically resolve and inject the associated configuration object.
/// </example>
[AttributeUsage(AttributeTargets.Property|AttributeTargets.Field)]
public class ConfigAttribute : Attribute
{
    /// <summary>
    /// The file name to use for configuration loading and saving. If null or empty, the default is derived from the type name.
    /// </summary>
    public string? FileName { get; }

    /// Represents an attribute used to decorate properties or fields to associate them with a specific configuration file.
    /// Use this attribute to specify an optional file name for a property or field, enabling it to be mapped to a configuration source.
    /// This attribute can be applied only to properties or fields of a class.
    /// Target: Property, Field
    /// Remarks:
    /// - The `FileName` property can optionally be used to specify the name of the configuration file associated with the property or field. If null, default behaviors may apply.
    public ConfigAttribute(string? fileName = null)
        => FileName = fileName;
}
