using System.Net;

namespace FirewallCore.Core;

public class GeoBlockManger : IDisposable
{
        private readonly string _configFolder;
        private readonly string _blockedCountriesFile;

        // retains an existing blocked‐country list
        private readonly HashSet<string> _blockedCountries = new(StringComparer.OrdinalIgnoreCase);

        // new in‐memory prefix database loaded from .zone files under GeoBlock
        private readonly List<(IPNetwork Network, string Country)> _prefixes = new();

        public GeoBlockManger(
            string configFolder         = "GeoBlock",
            string blockedCountriesFile = "blocked_countries.txt")
        {
            _configFolder         = configFolder;
            _blockedCountriesFile = Path.Combine(_configFolder, blockedCountriesFile);

            EnsureFilesExist();
            LoadBlockedCountries();
            LoadZones();      // ← load all “*.zone” files under GeoBlock/zones/ This data is set up with a simple bash script
        }

        private void EnsureFilesExist()
        {
            if (!Directory.Exists(_configFolder))
                Directory.CreateDirectory(_configFolder);

            // ensure we have a folder in .zone files
            var zonesDir = Path.Combine(_configFolder, "zones");
            if (!Directory.Exists(zonesDir))
                Directory.CreateDirectory(zonesDir);

            if (!File.Exists(_blockedCountriesFile))
                File.WriteAllLines(_blockedCountriesFile, new[]
                {
                    "# List ISO country codes (one per line) to block:",
                    "#CN",
                    "#RU",
                    "#KP",
                    "#US",
                    "#GB",
                    "#DE",
                    "#FR",
                    "#IT",
                    "#ES",
                    "#JP",
                });
        }

        private void LoadZones()
        {
            var zonesDir = Path.Combine(_configFolder, "zones");
            foreach (var path in Directory.EnumerateFiles(zonesDir, "*.zone"))
            {
                var cc = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
                foreach (var line in File.ReadLines(path))
                {
                    var txt = line.Trim();
                    if (string.IsNullOrEmpty(txt) || txt.StartsWith("#"))
                        continue;
                    if (IPNetwork.TryParse(txt, out var network))
                        _prefixes.Add((network, cc));
                }
            }

            _prefixes.Sort((a, b) => b.Network.PrefixLength.CompareTo(a.Network.PrefixLength));
        }

        private void LoadBlockedCountries()
        {
            _blockedCountries.Clear();
            foreach (var line in File.ReadAllLines(_blockedCountriesFile))
            {
                var t = line.Trim();
                if (t.StartsWith('#') || string.IsNullOrWhiteSpace(t)) continue;
                _blockedCountries.Add(t.ToUpperInvariant());
            }
        }

        /// <summary>
        /// Look up the country for the given IP purely via the .zone prefixes.
        /// </summary>
        public string GetCountry(string ip)
        {
            if (!IPAddress.TryParse(ip, out var addr))
                return "Unknown";

            foreach (var (network, country) in _prefixes)
            {
                if (network.Contains(addr))
                    return country;
            }

            return "Unknown";
        }

        public bool IsBlockedCountry(string ip)
        {
            var country = GetCountry(ip);
            return !string.IsNullOrEmpty(country) && _blockedCountries.Contains(country);
        }

        public void Dispose()
        {
            // nothing special to dispose
        }
}