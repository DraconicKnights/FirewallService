using DragonUtilities.Enums;
using FirewallCore.Core.Config;

namespace FirewallCore.Core;

internal class FirewallRuleDeployer
{
    public async void DeployPresetRules(IptablesManager iptablesManager, FirewallConfig config)
    {
            // Grab the valid SSH port via multiple choices
            int sshPort = GetSshPort();
            
            // Set the Config SSH port value to the chosen port
            config.FirewallSettings.ssh.Port = sshPort;
            
            // Save the new data into the config file
            var fileManager = new FileManager<FirewallConfig>(
                crypto: null,
                defaultFormat: FileFormat.Yaml);
            
            await fileManager.SaveAsync(
                "firewallconfig",
                config,
                secure: false,
                forceFormat: FileFormat.Yaml,
                headerComment: FirewallConfigExtensions.DefaultHeader);
            
            // Allow incoming connections to avoid connection hangs.
            iptablesManager.ExecuteCommand("-P INPUT ACCEPT");
            // Clear existing rules.
            iptablesManager.ExecuteCommand("-F INPUT");

            string allowRule = $"-I INPUT 1 -p tcp --dport {sshPort} -m conntrack --ctstate NEW,ESTABLISHED -j ACCEPT";
            iptablesManager.ExecuteCommand(allowRule);
            FirewallServiceProvider.Instance.LogAction($"Added allow rule for TCP port {sshPort}: {allowRule}", LogLevel.DEBUG);
            
            string commandConnectionRule = $"-A INPUT -p tcp -s 127.0.0.1 --dport 53860 -j ACCEPT";
            iptablesManager.ExecuteCommand(commandConnectionRule);

            string dropRule = $"-A INPUT -p tcp --dport {sshPort} -m state --state NEW -m recent --update --seconds {config.FirewallSettings.ssh.Seconds} --hitcount {config.FirewallSettings.ssh.Hitcount} --name SSH --rsource -j DROP";
            iptablesManager.ExecuteCommand(dropRule);
            
            FirewallServiceProvider.Instance.LogAction($"Added drop rule for TCP port {sshPort}: {dropRule}", LogLevel.DEBUG);
    }

    public void DeployDefaultRules()
    {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string folderPath = Path.Combine(baseDir, "FirewallRuleSet");
            string rulesFilePath = Path.Combine(folderPath, "rules.txt");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                FirewallServiceProvider.Instance.LogAction($"Created folder for firewall rules: {folderPath}", LogLevel.DEBUG);
            }

            if (!File.Exists(rulesFilePath))
            {
                
                string defaultContent =
                    "# Default firewall rules file\n" +
                    "\n" +
                    "# Log Connection Rules\n" +
                    "-I INPUT -m state --state NEW -j LOG --log-prefix \"New Connection: \"\n" +
                    "-A INPUT -p tcp -m state --state NEW -j LOG --log-prefix \"New TCP connection: \"\n" +
                    "-A INPUT -p udp -m state --state NEW -j LOG --log-prefix \"New UDP connection: \"\n" +
                    "\n" +
                    "# Loopback Rules\n" +
                    "-A INPUT -i lo -j ACCEPT\n" +
                    "-A INPUT -m conntrack --ctstate INVALID -j DROP\n" +
                    "-A INPUT -m conntrack --ctstate ESTABLISHED,RELATED -j ACCEPT\n" +
                    "-A OUTPUT -m conntrack --ctstate ESTABLISHED,RELATED -j ACCEPT\n" +
                    "\n" +
                    "# Ping Info\n" +
                    "-A INPUT -p icmp --icmp-type echo-request -j ACCEPT\n" +
                    "-A OUTPUT -p icmp --icmp-type echo-reply -j ACCEPT\n" +
                    "-A INPUT -p icmp --icmp-type echo-reply -j ACCEPT\n" +
                    "-A OUTPUT -p icmp --icmp-type echo-request -j ACCEPT\n" +
                    "\n" +
                    "# HTTP Info\n" +
                    "-A INPUT -p tcp -m multiport --dports 80,443 -j ACCEPT\n" +
                    "-A INPUT -p udp -m multiport --dports 80,443 -j ACCEPT\n" +
                    "\n" +
                    "# Firewall Service Command Listener\n" +
                    "-A INPUT -p tcp --dport 53860 -j ACCEPT\n\n";
                File.WriteAllText(rulesFilePath, defaultContent);
            }

            FirewallServiceProvider.Instance.LogAction($"Deploying preset file-based firewall rules from file: {rulesFilePath}", LogLevel.DEBUG);
            string[] ruleLines = File.ReadAllLines(rulesFilePath);
            foreach (string line in ruleLines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;
                FirewallServiceProvider.Instance.LogAction($"Executing rule: {trimmedLine}", LogLevel.DEBUG);
                FirewallServiceProvider.Instance.IptablesManager.ExecuteCommand(trimmedLine);
            }
    }

        public void DeployCustomRules()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string folderPath = Path.Combine(baseDir, "FirewallRuleSet");
            string customRulesFilePath = Path.Combine(folderPath, "custom_rules.txt");

            if (!File.Exists(customRulesFilePath))
            {
                string customContent =
                    "# Custom firewall rules\n" +
                    "# For example:\n" +
                    "# -A INPUT -p icmp -j ACCEPT\n";
                File.WriteAllText(customRulesFilePath, customContent);
                FirewallServiceProvider.Instance.LogAction($"Created default custom firewall rules file: {customRulesFilePath}", LogLevel.DEBUG);
            }

            FirewallServiceProvider.Instance.LogAction($"Deploying custom file-based firewall rules from file: {customRulesFilePath}", LogLevel.DEBUG);
            string[] customRuleLines = File.ReadAllLines(customRulesFilePath);
            foreach (string line in customRuleLines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;
                FirewallServiceProvider.Instance.LogAction($"Executing custom rule: {trimmedLine}", LogLevel.DEBUG);
                FirewallServiceProvider.Instance.IptablesManager.ExecuteCommand(trimmedLine);
            }
        }

        public void DeployOverrideBlockRule(IptablesManager iptablesManager)
        {
            iptablesManager.ExecuteCommand("-P INPUT DROP");
        }

        // May be changed to direct env support soon.
        private int GetSshPort()
        {
            int defaultPort = 55879;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string configFile = Path.Combine(baseDir, "ssh_port.config");
        
            // Try to get the value from the configuration file.
            if (File.Exists(configFile))
            {
                string content = File.ReadAllText(configFile).Trim();
                if (int.TryParse(content, out int filePort))
                {
                    return filePort;
                }
            }
        
            // Try to get the value from the environment variable.
            string envPort = Environment.GetEnvironmentVariable("SSH_PORT");
            if (!string.IsNullOrEmpty(envPort) && int.TryParse(envPort, out int envPortNumber))
            {
                return envPortNumber;
            }
        
            // Try to prompt the user for the port number if in an interactive context.
            // Allow the user to press Enter to accept the default.
            if (!Console.IsInputRedirected)
            {
                Console.WriteLine($"Please enter the port number for SSH connections (or press Enter to use the default {defaultPort}):");
                while (true)
                {
                    string input = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(input))
                    {
                        // User pressed Enter without an input; use the default.
                        File.WriteAllText(configFile, defaultPort.ToString());
                        return defaultPort;
                    }
                    if (int.TryParse(input.Trim(), out int portNumber))
                    {
                        File.WriteAllText(configFile, portNumber.ToString());
                        return portNumber;
                    }
                    Console.WriteLine("Invalid port number. Please try again (or press Enter to use the default):");
                }
            }
        
            // Fallback: if none of the above options are available, return the default.
            return defaultPort;
        }
}