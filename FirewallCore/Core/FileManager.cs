using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FirewallCore.Utils;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

// where CryptoService lives

namespace FirewallCore.Core
{
    public enum FileFormat { Json, Yaml }

    public class FileManager<T> where T : new()
    {
        private readonly string _rootDir;
        private readonly CryptoService _crypto;
        private readonly FileFormat _defaultFormat;
        
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        
        private readonly ISerializer   _yamlSerializer;
        private readonly IDeserializer _yamlDeserializer;

        /// <summary>
        /// </summary>
        /// <param name="crypto">
        ///   If provided, secure load/save will use AES via this service.
        ///   Otherwise falls back to Windows DPAPI.
        /// </param>
        /// <param name="defaultFormat">Which format to pick if the name has no extension.</param>
        public FileManager(CryptoService? crypto = null,
                           FileFormat defaultFormat = FileFormat.Json)
        {
            _crypto = crypto;
            _defaultFormat = defaultFormat;

            _rootDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                    "FirewallConfig");
            Directory.CreateDirectory(_rootDir);

            var yamlBldr = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(
                    DefaultValuesHandling.OmitNull);
            _yamlSerializer   = yamlBldr.Build();
            _yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
        }
        
        // NEW overload: lets you supply any root directory
        public FileManager(string rootDir, CryptoService? crypto = null, FileFormat defaultFormat = FileFormat.Json)
        {
            _crypto = crypto;
            _defaultFormat = defaultFormat;
            _rootDir = rootDir;
            Directory.CreateDirectory(_rootDir);

            var yamlBldr = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(
                    DefaultValuesHandling.OmitNull);
            
            _yamlSerializer = yamlBldr.Build();
            _yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
        }
        
        public T Load(string name)
        {
            var path    = ResolvePath(name, null);
            var rawText = File.ReadAllText(path);

            if (_crypto != null)
                rawText = _crypto.Decrypt(rawText);

            var fmt = DetermineFormat(path, null);
            return Deserialize(rawText, fmt);
        }

        public void Save(string name, T obj)
        {
            var path = ResolvePath(name, null);
            var fmt  = DetermineFormat(path, null);
            var body = Serialize(obj, fmt, headerComment: null);

            if (_crypto != null)
                body = _crypto.Encrypt(body);

            File.WriteAllText(path, body, Encoding.UTF8);
        }

        public async Task<T> LoadAsync(string name,
                                       bool secure = false,
                                       FileFormat? forceFormat = null)
        {
            var path    = ResolvePath(name, forceFormat);
            var rawText = await File.ReadAllTextAsync(path, Encoding.UTF8);

            if (secure && _crypto != null)
                rawText = _crypto.Decrypt(rawText);

            var fmt = DetermineFormat(path, forceFormat);
            return Deserialize(rawText, fmt);
        }

        public async Task SaveAsync(string name,
                                    T obj,
                                    bool secure = false,
                                    FileFormat? forceFormat = null,
                                    string headerComment = null)
        {
            var path = ResolvePath(name, forceFormat);
            var fmt  = DetermineFormat(path, forceFormat);
            
            var body = Serialize(obj, fmt, headerComment);

            if (secure && _crypto != null)
                body = _crypto.Encrypt(body);

            await File.WriteAllTextAsync(path, body, Encoding.UTF8);
        }

        private T Deserialize(string text, FileFormat fmt)
        {
            return fmt switch
            {
                FileFormat.Json => JsonSerializer.Deserialize<T>(text, _jsonOptions)!,
                FileFormat.Yaml => _yamlDeserializer.Deserialize<T>(text)!,
                _               => throw new InvalidOperationException()
            };
        }

        private string Serialize(T obj, FileFormat fmt, string headerComment)
        {
            switch (fmt)
            {
                case FileFormat.Json:
                    return JsonSerializer.Serialize(obj, _jsonOptions);

                case FileFormat.Yaml:
                    var sb = new StringBuilder();
                    if (!string.IsNullOrWhiteSpace(headerComment))
                        foreach (var line in headerComment.Split('\n'))
                            sb.AppendLine("# " + line.TrimEnd());

                    // raw YAML
                    var rawYaml = _yamlSerializer.Serialize(obj);
                    // inject per-property descriptions
                    var descs   = CollectDescriptions(typeof(T));
                    var withComments = InjectPropertyComments(rawYaml, descs);

                    sb.Append(withComments);
                    return sb.ToString();


                default:
                    throw new InvalidOperationException();
            }
        }
        
        private static string InjectPropertyComments(string yaml,
            Dictionary<string, string> descriptions)
        {
            var output = new StringBuilder();
            foreach (var line in yaml.Split('\n'))
            {
                // detect a property line: "<indent>key: ..."
                var trimmed = line.TrimStart();
                if (trimmed.Length > 0 && trimmed.Contains(':'))
                {
                    var key = trimmed.Substring(0, trimmed.IndexOf(':')).Trim();
                    if (descriptions.TryGetValue(key, out var desc))
                    {
                        // preserve indent
                        var indent = line.Substring(0, line.Length - trimmed.Length);
                        output.AppendLine(indent + "# " + desc);
                    }
                }
                output.AppendLine(line);
            }
            return output.ToString();
        }
        
        private static Dictionary<string, string> CollectDescriptions(Type type)
        {
            var dict = new Dictionary<string, string>();
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var key = ToCamelCase(prop.Name);
                if (prop.GetCustomAttribute<DescriptionAttribute>() is DescriptionAttribute da)
                    dict[key] = da.Description;

                if (prop.PropertyType.IsClass
                    && prop.PropertyType != typeof(string))
                {
                    foreach (var kv in CollectDescriptions(prop.PropertyType))
                        dict[kv.Key] = kv.Value;
                }
            }
            return dict;
        }
        
        private static string ToCamelCase(string name) =>
            string.IsNullOrEmpty(name)
                ? name
                : char.ToLowerInvariant(name[0]) + name.Substring(1);

        private string ResolvePath(string name, FileFormat? forceFormat)
        {
            var ext = Path.GetExtension(name);
            if (string.IsNullOrEmpty(ext))
            {
                ext = (forceFormat ?? _defaultFormat) == FileFormat.Yaml
                    ? ".yaml"
                    : ".json";
                name += ext;
            }

            return Path.Combine(_rootDir, name);
        }

        private FileFormat DetermineFormat(string path,
                                           FileFormat? forceFormat)
        {
            if (forceFormat.HasValue)
                return forceFormat.Value;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".yaml" or ".yml" => FileFormat.Yaml,
                _ => FileFormat.Json
            };
        }
    }
}