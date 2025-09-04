using System.Runtime.InteropServices;
using FirewallCore.Utils;
using FirewallInterface.Interface;

namespace FirewallCore.Core;

public class PluginConfigManager : IPluginConfigManager
{
    private readonly CryptoService? _crypto;
    private readonly FileFormat    _format;

    public PluginConfigManager(CryptoService? crypto, FileFormat format)
    {
        _crypto = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? crypto : null;
        _format = format;
    }

    public T LoadConfig<T>(string pluginName, string configName) where T : new()
    {
        var pluginDir = EnsurePluginDir(pluginName);
        var path      = GetConfigPath(pluginName, configName);

        // if there's no file, save a default and return it
        if (!File.Exists(path))
        {
            var cfg = new T();
            SaveConfig(pluginName, configName, cfg);
            return cfg;
        }
        
        var fm = new FileManager<T>(pluginDir, _crypto, _format);
        return fm.Load(configName);
    }

    public void SaveConfig<T>(string pluginName, string configName, T config) where T : new()
    {
        var pluginDir = EnsurePluginDir(pluginName);
        var path      = GetConfigPath(pluginName, configName);
        
        var fm = new FileManager<T>(pluginDir, _crypto, _format);
        fm.Save(configName, config);
    }
    
    
    // 1) Exists?
    public bool ConfigExists(string plugin, string name)
    {
        var path = GetConfigPath(plugin, name);
        return File.Exists(path);
    }

    // 2) Delete
    public void DeleteConfig(string plugin, string name)
    {
        var path = GetConfigPath(plugin, name);
        if (File.Exists(path)) File.Delete(path);
    }

    // 3) List all config file names (without extension)
    public IEnumerable<string> ListConfigs(string plugin)
    {
        var dir = EnsurePluginDir(plugin);
        foreach (var file in Directory.EnumerateFiles(dir))
        {
            yield return Path.GetFileNameWithoutExtension(file);
        }
    }

    // 4) Load or use default factory
    public T LoadOrDefault<T>(string plugin, string name, Func<T> fallback) where T : new()
    {
        if (ConfigExists(plugin, name))
            return LoadConfig<T>(plugin, name);
        var def = fallback();
        SaveConfig(plugin, name, def);
        return def;
    }

    // 5) Backup single file or entire folder
    public void BackupConfig(string plugin, string name, string backupDir)
    {
        var src = GetConfigPath(plugin, name);
        var dst = Path.Combine(backupDir, plugin, Path.GetFileName(src));
        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
        File.Copy(src, dst, overwrite: true);
    }

    public void BackupAllConfigs(string plugin, string backupDir)
    {
        var srcDir = EnsurePluginDir(plugin);
        var dstDir = Path.Combine(backupDir, plugin);
        Directory.CreateDirectory(dstDir);
        foreach (var file in Directory.EnumerateFiles(srcDir))
        {
            File.Copy(file, Path.Combine(dstDir, Path.GetFileName(file)), overwrite: true);
        }
    }

    // Helpers
    private string EnsurePluginDir(string plugin)
    {
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", plugin, "Config");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string GetConfigPath(string plugin, string name)
    {
        var ext = _format == FileFormat.Json ? ".json" : ".yaml";
        return Path.Combine(EnsurePluginDir(plugin), name + ext);
    }
}
