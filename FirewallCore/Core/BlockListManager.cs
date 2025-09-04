using System.Net;
using FirewallEvent.Events.Core;
using FirewallEvent.Events.EventCalls;
using FirewallInterface.Interface;

namespace FirewallCore.Core
{
    /// <summary>
    /// Manages blocklist and whitelist files used to control IP blocking.
    /// </summary>
    internal class BlockListManager : IBlockListManager
    {
        public string BlocklistFolder { get; }
        public string WhitelistFolder { get; }
        public string BlocklistPath { get; }
        public string WhitelistPath { get; }

        // In-memory sets to quick check lists.
        public HashSet<string> BlockedIPs { get; } = new HashSet<string>();
        public HashSet<string> WhitelistedIPs { get; } = new HashSet<string>();

        public BlockListManager(
            string blocklistFolder = "BlockList", 
            string whitelistFolder = "Whitelist", 
            string blocklistFileName = "blocklist.txt", 
            string whitelistFileName = "whitelist.txt")
        {
            BlocklistFolder = blocklistFolder;
            WhitelistFolder = whitelistFolder;
            BlocklistPath = Path.Combine(blocklistFolder, blocklistFileName);
            WhitelistPath = Path.Combine(whitelistFolder, whitelistFileName);
            EnsureFilesExist();
            LoadLists();
        }

        /// <summary>
        /// Creates the necessary directories and text files with sample content if they don't exist.
        /// </summary>
        private void EnsureFilesExist()
        {
            // Create directories if they don't exist.
            if (!Directory.Exists(BlocklistFolder))
            {
                Directory.CreateDirectory(BlocklistFolder);
            }
            if (!Directory.Exists(WhitelistFolder))
            {
                Directory.CreateDirectory(WhitelistFolder);
            }

            if (!File.Exists(BlocklistPath))
            {
                var sampleBlocks = new List<string>
                {
                    "#Example Block IP Addresses:",
                    "#8.8.8.8",
                };
                File.WriteAllLines(BlocklistPath, sampleBlocks);
            }

            if (!File.Exists(WhitelistPath))
            {
                var sampleWhitelist = new List<string>
                {
                    "#Example Whitelist IP Addresses:",
                    "#8.8.8.8",
                };
                File.WriteAllLines(WhitelistPath, sampleWhitelist);
            }
        }

        /// <summary>
        /// Loads IP addresses from the blocklist and whitelist files into in-memory collections.
        /// Only valid IP addresses that are not commented out (lines not starting with '#') are loaded.
        /// </summary>
        public void LoadLists()
        {
            BlockedIPs.Clear();
            WhitelistedIPs.Clear();

            if (File.Exists(BlocklistPath))
            {
                foreach (var line in File.ReadAllLines(BlocklistPath))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                        continue;

                    // Validate the IP address.
                    if (IPAddress.TryParse(trimmed, out _))
                        BlockedIPs.Add(trimmed);
                }
            }

            if (File.Exists(WhitelistPath))
            {
                foreach (var line in File.ReadAllLines(WhitelistPath))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                        continue;

                    // Validate the IP address.
                    if (IPAddress.TryParse(trimmed, out _))
                        WhitelistedIPs.Add(trimmed);
                }
            }
        }


        IReadOnlyCollection<string> IBlockListManager.BlockedIPs => BlockedIPs;
        IReadOnlyCollection<string> IBlockListManager.WhitelistedIPs => WhitelistedIPs;

        /// <summary>
        /// Checks if an IP is in the whitelist.
        /// </summary>
        public bool IsWhitelisted(string ip)
        {
            return WhitelistedIPs.Contains(ip);
        }

        /// <summary>
        /// Checks if an IP is in the blocklist.
        /// </summary>
        public bool IsBlocked(string ip)
        {
            return BlockedIPs.Contains(ip);
        }

        public void Add(string ip)
        {
            FirewallEventService.Instance.Publish(new WhitelistAddedEvent(ip));
            WhitelistedIPs.Add(ip);
            File.AppendAllLines(WhitelistPath, new[] { ip });
        }

        public void Remove(string ip)
        {
            FirewallEventService.Instance.Publish(new WhitelistRemovedEvent(ip));
            WhitelistedIPs.Remove(ip);
            var lines = File.ReadAllLines(WhitelistPath).Where(l => l.Trim() != ip).ToArray();
            File.WriteAllLines(WhitelistPath, lines);
        }
    }
}