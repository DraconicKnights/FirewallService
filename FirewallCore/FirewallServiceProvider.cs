using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DragonUtilities.Controller;
using DragonUtilities.Enums;
using DragonUtilities.Interfaces;
using FirewallCore.Core;
using FirewallCore.Core.Config;
using FirewallCore.Core.Tasks;
using FirewallCore.Utils;
using FirewallEvent.Events.Core;
using FirewallEvent.Events.EventCalls;
using FirewallInterface.Interface;

namespace FirewallCore
{
    internal class FirewallServiceProvider : BaseController, IFirewallContext
    {
        private static FirewallConfig _config;
        private TcpListener _listener;
        private X509Certificate2 _certificate;
        
        // Global constants.
        private const string SyslogPath = "/var/log/syslog";
        public const string ConnectionLogPath = "connection_attempts.log";
        public const string LogArchiveDir = "ServerConnectionLogs";
        
        // Dictionaries for tracking connection attempts and blocked IPs.
        internal static readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> IpAttempts = new();

        internal static readonly ConcurrentDictionary<string, DateTime> BlockedIPs = new();
        
        // Collection for whitelisted IP addresses.
        internal static readonly HashSet<string> Whitelist = new HashSet<string>();
        
        private readonly ConcurrentDictionary<string, Guid> _ipIdentifiers = new();

        // Pre-compiled regex to extract log details.
        private static readonly Regex SyslogPrefix = new(@"^\w{3}\s+\d+\s+\d{2}:\d{2}:\d{2}\s+\S+\s+\S+\[\d+\]:\s*", RegexOptions.Compiled);
        private static readonly Regex connectionRegex = new Regex(@"(New (TCP|UDP) connection:)", RegexOptions.Compiled);
        private static readonly Regex srcRegex = new Regex(@"SRC=([^ ]+)", RegexOptions.Compiled);
        private static readonly Regex sptRegex = new Regex(@"SPT=([^ ]+)", RegexOptions.Compiled);
        private static readonly Regex dptRegex = new Regex(@"DPT=([^ ]+)", RegexOptions.Compiled);
        
        public CommandManager CommandManager { get; }
        public string GetSysLogPath { get; } = SyslogPath;
        public IptablesManager IptablesManager { get; }
        public FirewallRuleDeployer RuleDeployer { get; }
        public SyslogMonitor SyslogMonitor { get; }
        public LogManager LogManager { get; }
        public FirewallRuntimeManager RuntimeManager { get; }
        public BlockListManager BlockListManager { get; }
        public DatabaseManager DatabaseManager { get; }
        public GeoBlockManger GeoBlockManager { get; }
        public PluginManger PluginManager { get; }
        public SchedulerService SchedulerService { get; }
        public static FirewallServiceProvider Instance { get; private set; }
        
        public FirewallServiceProvider(FirewallConfig config)
        {
            Instance = this;

            _config = config;
            
            IptablesManager = new IptablesManager();
            DatabaseManager = new DatabaseManager();
            CommandManager = new CommandManager();
            SyslogMonitor = new SyslogMonitor();
            LogManager = new LogManager();
            RuntimeManager = new FirewallRuntimeManager();
            BlockListManager = new BlockListManager();
            RuleDeployer = new FirewallRuleDeployer();
            GeoBlockManager = new GeoBlockManger();
            SchedulerService = new SchedulerService();
            PluginManager = new PluginManger();

            Console.Clear();
            
            var now = DateTime.UtcNow;
            var proc = Process.GetCurrentProcess();
            var os   = Environment.OSVersion;
            
            var lines = new[]
            {
                "🔥 Firewall Service Initialization 🔥",
                "",
                $"Service Name           : {nameof(FirewallServiceProvider)}",
                "Author                  : Dragonoid",
                "Timestamp               :" + now.ToString("yyyy-MM-dd HH:mm:ss") + " UTC",
                $"Process ID             : {proc.Id}",
                $"OS                     : {os}", 
                $"Syslog path            : {GetSysLogPath}",
                $".NET runtime           : {Environment.Version}",
                $"CPU Count              : {Environment.ProcessorCount}",
                $"Whitelisted IPs        : {BlockListManager.WhitelistedIPs.Count}",
                $"Currently blocked IPs  : {BlockListManager.BlockedIPs.Count}",
                $"Thresholds             : {ThresholdAttempts} attempts in {ThresholdSeconds}s",
                $"Default block duration : {DefaultBlockDurationSeconds}s",
            };
            
            LogRaw(MessageUtiliity.BuildAsciiBox(lines));;
            
            _crypto = new CryptoService(keyString: config.FirewallSettings.Crypto.Key, ivString: config.FirewallSettings.Crypto.Iv);
            
            var certCfg = _config.FirewallSettings.Certificate;
            
            string certificatePath = Path.Combine(Environment.CurrentDirectory, certCfg.Path);
            _certificate = new X509Certificate2(certificatePath, certCfg.Password);
            
            FirewallEventService.Instance.Subscribe<BlockEvent>(HandleBLockEvent);
            FirewallEventService.Instance.Subscribe<UnblockEvent>(HandleUnBlockEvent);
        }

        private void HandleUnBlockEvent(UnblockEvent obj)
        {
            LogAction($"IP Address {obj.IpAddress} has been Unblocked", LogLevel.WARNING);
        }

        private void HandleBLockEvent(BlockEvent obj)
        {
            LogAction($"IP Address {obj.IpAddress} blocked for {obj.DurationSeconds} seconds.", LogLevel.WARNING);
        }

        public void LogAction(string message, LogLevel level = LogLevel.INFO)
        {
            Logger.Log(message, level);
        }

        public void LogRaw(string message) => Console.WriteLine(message);
        
        private string StripPrefix(string line)
        {
            return SyslogPrefix.Replace(line, "");
        }
        
        // Processes a single log line.
        public void ProcessLogLine(string rawLine)
        {
            var line = StripPrefix(rawLine);
            if (!connectionRegex.IsMatch(line))
                return;

            // Extract source IP
            var srcMatch = srcRegex.Match(line);
            var srcAddress = srcMatch.Success ? srcMatch.Groups[1].Value : "";
            if (srcAddress == "127.0.0.1")
                return;

            var ipId = GetIdentifierForIp(srcAddress);

            // Whitelist check
            if (BlockListManager.IsWhitelisted(srcAddress))
            {
                LogAction($"[WHITELIST] {DateTime.Now:yyyy-MM-dd HH:mm:ss} IP={srcAddress}", LogLevel.DEBUG);
                return;
            }
            
            FirewallEventService.Instance.Publish(new ConnectionAttemptEvent(srcAddress));

            // Geo-lookup (cached) and geo-block check
            var country = GeoBlockManager.GetCountry(srcAddress);
            if (GeoBlockManager.IsBlockedCountry(srcAddress))
            {
                FirewallEventService.Instance.Publish(new GeoBlockEvent(srcAddress, country));;
                
                var ts = DateTime.Now;
                IptablesManager.BlockIP(srcAddress, LogAction, DefaultBlockDurationSeconds);
                LogAction(
                    $"[GEO-BLOCK] {ts:yyyy-MM-dd HH:mm:ss} IP={srcAddress} Country={country} Action=BLOCKED Duration={DefaultBlockDurationSeconds}s",
                    LogLevel.WARNING
                );
                return;
            }

            // Extract ports
            var srcPort = sptRegex.Match(line).Groups[1].Value;
            var dstPort = dptRegex.Match(line).Groups[1].Value;
            var now = DateTime.Now;

            // Record and prune attempt history
            var queue = IpAttempts.GetOrAdd(srcAddress, _ => new ConcurrentQueue<DateTime>());
            queue.Enqueue(now);

            // Prune any timestamps older than our threshold window
            var windowStart = now.AddSeconds(-ThresholdSeconds);
            while (queue.TryPeek(out var oldest) && oldest < windowStart)
            {
                queue.TryDequeue(out _);
            }

            // Now you can inspect queue.Count for total recent attempts
            int attempts = queue.Count;

            var first = IpAttempts[srcAddress].Min();
            var window = (now - first).TotalSeconds;

            if (TryParseConnectionAttempt(line, out var connectionAttempt))
            {
                FirewallEventService.Instance.Publish(new ConnectionAttemptEvent(connectionAttempt));
            }

            // reverse DNS lookup
            string host = "n/a";
            try
            {
                host = Dns.GetHostEntry(srcAddress)?.HostName ?? "n/a";
            }
            catch
            {
                LogAction($"Failed to resolve reverse DNS for {srcAddress}", LogLevel.DEBUG);
            }

            var allowPlain = _config.Logging.AllowPlaintextCurrentLog;

            if (allowPlain)
            {
                // Build a richer log entry
                var logEntry = string.Join(" | ", new[]
                {
                    now.ToString("yyyy-MM-dd HH:mm:ss"),            // Timestamp
                    $"ID={ipId}",                                          // IP ID
                    $"PID={Process.GetCurrentProcess().Id}",               // Process ID
                    $"TID={Thread.CurrentThread.ManagedThreadId}",         // Thread ID
                    $"IP={srcAddress}",                                    // IP
                    $"Host={host}",                                        // Resolved reverse DNS
                    $"Country={country}",                                  // ISO country code
                    $"SrcPort={srcPort}",                                  // Source port
                    $"DstPort={dstPort}",                                  // Destination port
                    $"Attempts={attempts}",                                // Count in window
                    $"Window={window:F1}s"                                 // Time span of those attempts
                });
                LogManager.AppendLog(logEntry);

                /*// CSV-friendly version for reporting
                var csvLine = string.Join(',', new[]
                {
                    now.ToString("o"),
                    Process.GetCurrentProcess().Id.ToString(),
                    Thread.CurrentThread.ManagedThreadId.ToString(),
                    srcAddress,
                    host,
                    country,
                    srcPort,
                    dstPort,
                    attempts.ToString(),
                    window.ToString("F1")
                });
                File.AppendAllTextAsync(CsvLogPath, csvLine + Environment.NewLine);*/
                
                LogAction($"[INFO] {logEntry}", LogLevel.DEBUG);
            }
            
            // Threshold-based blocking (≥ ensures immediate block at limit)
            if (attempts >= ThresholdAttempts)
            {
                IptablesManager.BlockIP(srcAddress, LogAction, DefaultBlockDurationSeconds);
                LogAction(
                    $"[RATE-BLOCK] {now:yyyy-MM-dd HH:mm:ss} IP={srcAddress} Country={country} " +
                    $"Attempts={attempts}/{ThresholdAttempts} → BLOCKED",
                    LogLevel.WARNING
                );
            }
        }
        
        public void ExportLogs(string fileName)
        {
            // build & validate secure path
            var basePath = _config.Logging.SecureExportPath;
            Directory.CreateDirectory(basePath);
            var candidate = Path.Combine(basePath, fileName);
            var fullCandidate = Path.GetFullPath(candidate);
            if (!fullCandidate.StartsWith(Path.GetFullPath(basePath), StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Export path outside secure area.");

            // gather + serialize
            var lines = LogManager.GetAllLogs();
            var json   = JsonSerializer.Serialize(lines, new JsonSerializerOptions { WriteIndented = true });

            // encrypt using configured key/IV
            var cipher = EncryptToBytes(json,
                Convert.FromBase64String(_config.FirewallSettings.Crypto.Key),
                Convert.FromBase64String(_config.FirewallSettings.Crypto.Iv));

            // atomic write
            using var fs = new FileStream(fullCandidate, FileMode.Create, FileAccess.Write, FileShare.None);
            fs.Write(cipher, 0, cipher.Length);
        }

        public string ReadExportedLogs(string fileName)
        {
            var basePath = _config.Logging.SecureExportPath;
            var path     = Path.Combine(basePath, fileName);
            var cipher   = File.ReadAllBytes(path);

            try
            {
                return DecryptFromBytes(
                    cipher,
                    Convert.FromBase64String(_config.FirewallSettings.Crypto.Key),
                    Convert.FromBase64String(_config.FirewallSettings.Crypto.Iv)
                );
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException(
                    $"Unable to decrypt '{fileName}'. The file may be corrupted or the key/IV is invalid.", ex
                );
            }
        }

        private byte[] EncryptToBytes(string plainText, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV  = iv;

            using var mem = new MemoryStream();
            // make sure the CryptoStream (and its internal final block) is written before we snapshot the buffer
            using (var crypto = new CryptoStream(mem, aes.CreateEncryptor(), CryptoStreamMode.Write))
            using (var writer = new StreamWriter(crypto, Encoding.UTF8))
            {
                writer.Write(plainText);
            }
            // disposing `crypto` calls FlushFinalBlock internally, so now mem contains the full ciphertext
            return mem.ToArray();
        }

        private string DecryptFromBytes(byte[] cipherText, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key; aes.IV = iv;
            using var mem = new MemoryStream(cipherText);
            using var crypto = new CryptoStream(mem, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new StreamReader(crypto, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        
        private void LoadBlockList()
        {
            var blockedAddresses = DatabaseManager.GetBlockedIPs();
            
            foreach (var blocked in blockedAddresses)
            {
                ManualBlockIP(blocked.IP, blocked.DurationSeconds);
                var remainingTime = blocked.ScheduledUnblockTime - DateTime.Now;
                if (remainingTime.TotalMilliseconds <= 0)
                {
                    // If the unblock time has already passed, unblock immediately.
                    IptablesManager.UnblockIP(blocked.IP, LogAction);
                }
                else
                {
                    // Re-add the IP to the in-memory store and schedule a future unblock.
                    BlockedIPs[blocked.IP] = blocked.BlockedTime;
                    Task.Run(async () =>
                    {
                        await Task.Delay((int)remainingTime.TotalMilliseconds);
                        IptablesManager.UnblockIP(blocked.IP, LogAction);
                    });
                }
            }
        }
        
        // Manually blocks an IP.
        public void ManualBlockIP(string ip, int? durationSeconds = null)
        {
            if (BlockListManager.IsWhitelisted(ip))
            {
                LogAction($"Manual block request for whitelisted IP: {ip} was ignored.", LogLevel.INFO);
                return;
            }

            IptablesManager.BlockIP(ip, LogAction, durationSeconds ?? FirewallServiceProvider.DefaultBlockDurationSeconds);

            if (!BlockListManager.IsBlocked(ip))
            {
                BlockListManager.BlockedIPs.Add(ip);
            }
        }
        
        public string GetBlockedAddressesResponse()
        {
            var blockedAddresses = DatabaseManager.GetBlockedIPs();
            
            if (blockedAddresses.Count == 0)
            {
                LogAction("No currently blocked addresses.", LogLevel.INFO);
                return "No currently blocked addresses.";
            }
            else
            {
                var responseBuilder = new StringBuilder();
                foreach (var blocked in blockedAddresses)
                {
                    string entry = $"Blocked IP: {blocked.IP} | Blocked Time: {blocked.BlockedTime} | Duration: {blocked.DurationSeconds}s | Scheduled Unblock Time: {blocked.ScheduledUnblockTime}";
                    LogAction(entry, LogLevel.INFO);
                    responseBuilder.AppendLine(entry);
                }
                return responseBuilder.ToString();
            }
        }

        public string GetStatusResponse()
        {
            if (IpAttempts.Count == 0)
            {
                LogAction("No connection attempts recorded.", LogLevel.INFO);
                return "No connection attempts recorded.";
            }
            else
            {
                var responseBuilder = new StringBuilder();
                string header = "Connection attempt status:";
                LogAction(header, LogLevel.INFO);
                responseBuilder.AppendLine(header);
                foreach (var entry in IpAttempts)
                {
                    string details = $"IP: {entry.Key} | Recent Attempts: {entry.Value.Count}";
                    LogAction(details, LogLevel.INFO);
                    responseBuilder.AppendLine(details);
                }

                return responseBuilder.ToString();
            }
        }

        public void ReloadFirewallRules()
        {
            LogAction("Reloading firewall rules...", LogLevel.DEBUG);
            RuleDeployer.DeployPresetRules(IptablesManager, _config);
            RuleDeployer.DeployDefaultRules();
            RuleDeployer.DeployCustomRules();
            RuleDeployer.DeployOverrideBlockRule(IptablesManager);
            LoadBlockList();
            LogAction("Firewall rules reloaded.", LogLevel.DEBUG);
        }

        /// <summary>
        /// Starts a TCP command server. A client (like netcat) can connect
        /// to enter commands, which are then processed by the Command Manager.
        /// </summary>
        private async Task StartCommandServerAsync(CancellationToken token, int port = 53860)
        {
            TcpListener listener;

            // set up listener
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.Start();
                LogAction($"Command server listening on port {port}", LogLevel.DEBUG);

                while (!token.IsCancellationRequested)
                {
                    // accept next client (cancellable)
                    var client = await listener.AcceptTcpClientAsync(token);
                    _ = HandleClientAsync(client, token);
                }

                listener.Stop();
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                LogAction($"Failed to start command server on port {port}: {ex.Message}", LogLevel.ERROR);
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
        }
        
        /// <summary>
        /// One client connection: decide TLS vs plaintext, then loop on lines.
        /// </summary>
        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            var remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
            bool isLoopback = remoteEndPoint != null &&
                              IPAddress.IsLoopback(remoteEndPoint.Address);
            bool usePlaintext = _config.FirewallSettings.AllowPlainTextCommands
                                || isLoopback;

            Stream rawStream = client.GetStream();
            if (!usePlaintext)
            {
                // wrap in TLS
                var ssl = new SslStream(rawStream, leaveInnerStreamOpen: false);
                await ssl.AuthenticateAsServerAsync(
                    _certificate,
                    clientCertificateRequired: false,
                    enabledSslProtocols: SslProtocols.Tls13,
                    checkCertificateRevocation: false);
                rawStream = ssl;
            }

            using var reader = new StreamReader(rawStream, Encoding.UTF8, leaveOpen: true);
            await using var writer = new StreamWriter(rawStream, Encoding.UTF8, leaveOpen: true);
            writer.AutoFlush = true;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync();
                    if (line is null) break; // client closed

                    string command = line;
                    if (!usePlaintext)
                    {
                        command = _crypto.Decrypt(command);
                    }

                    // run your router
                    CommandManager.ProcessCommand(command, this, out var result);

                    if (!string.IsNullOrEmpty(result))
                    {
                        string outLine = usePlaintext
                            ? result
                            : _crypto.Encrypt(result);

                        await writer.WriteLineAsync(outLine);
                    }
                }
            }
            catch (Exception ex)
            {
                LogAction($"Error processing client {remoteEndPoint}: {ex.Message}", LogLevel.ERROR);
            }
            finally
            {
                client.Close();
            }
        }

        
        private async Task ProcessClientAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            {
                using (SslStream sslStream = new SslStream(client.GetStream(), false))
                {
                    try
                    {
                        // Authenticate as a server using the certificate.
                        await sslStream.AuthenticateAsServerAsync(
                            _certificate,
                            clientCertificateRequired: false,
                            enabledSslProtocols: SslProtocols.Tls13,
                            checkCertificateRevocation: true);

                        byte[] buffer = new byte[4096];
                        int bytesRead;

                        while ((bytesRead = await sslStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                        {
                            // Convert the incoming bytes into a command string.
                            string command = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                            if (!string.IsNullOrEmpty(command))
                            {
                                try
                                {
                                    string decryptedCommand = _crypto.Decrypt(command);
                                    command = decryptedCommand;
                                }
                                catch (Exception e)
                                {
                                    LogAction($"Error decrypting command: {e.Message}");
                                    throw;
                                }
                            }

                            // Process the command using your command processing logic.
                            CommandManager.ProcessCommand(command, this, out string result);

                            // Encrypt the response if it's not empty.
                            if (!string.IsNullOrEmpty(result))
                            {
                                string encryptedResponse = _crypto.Encrypt(result);
                                byte[] responseBytes = Encoding.UTF8.GetBytes(encryptedResponse + "\n");
                                await sslStream.WriteAsync(responseBytes, 0, responseBytes.Length, token);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log exceptions as needed.
                        LogAction($"Error processing client: {ex.Message}");
                    }
                }
            }
        }
        
        // Asynchronous console command handler.
        // This is only used if running interactively.
        // It's not used in the background service.'
        private async Task StartCommandHandlerAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    string input = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(input))
                    {
                        CommandManager.ProcessCommand(input, this, out string response);
                        LogAction(response, LogLevel.INFO);
                    }
                }
                await Task.Delay(200, token);
            }
        }
        
        /// <summary>
        /// Very simple parser stub: return true if this line represents an "attempt" for IP.
        /// Replace this with your real pattern matching or syslog parsing.
        /// </summary>
        public bool TryParseConnectionAttempt(string line, out string ip)
        {
            // e.g. if log lines look like "ATTEMPT src=1.2.3.4", etc.
            const string tag = "ATTEMPT src=";
            if (line.StartsWith(tag, StringComparison.OrdinalIgnoreCase))
            {
                ip = line.Substring(tag.Length).Split(' ')[0];
                return true;
            }

            ip = null!;
            return false;
        }
        
        // Generates or returns an existing identifier for this IP
        public Guid GetIdentifierForIp(string ip)
        {
            if (_ipIdentifiers.TryGetValue(ip, out var identifier))
            {
                return identifier;
            }

            identifier = Guid.NewGuid();
            _ipIdentifiers[ip] = identifier;
            return identifier;
        }
        
        IEnumerable<(DateTime Time, string Message)> IFirewallContext.GetHistory(Guid ipGuid)
        {
            return DatabaseManager.GetHistoryForGuid(ipGuid);
        }

        void IFirewallContext.ClearHistory(Guid ipGuid)
        {
            DatabaseManager.ClearHistory(ipGuid);
        }

        (int totalAttempts, int recentFails, DateTime lastSeen) IFirewallContext.GetStatsForGuid(Guid ipGuid)
        {
            return DatabaseManager.GetStatsForGuid(ipGuid);
        }
        
        public void AddTag(Guid ipGuid, string tag)
        {
            DatabaseManager.InsertTag(ipGuid, tag);
        }

        public void RemoveTag(Guid ipGuid, string tag)
        {
            DatabaseManager.DeleteTag(ipGuid, tag);
        }

        public IEnumerable<string> GetTags(Guid ipGuid)
        {
            return DatabaseManager.GetTagsForGuid(ipGuid);
        }
        
        void IFirewallContext.AddComment(Guid ipGuid, string comment)
        {
            DatabaseManager.InsertComment(ipGuid, comment);
        }

        IEnumerable<(DateTime Time, string Comment)> IFirewallContext.GetComments(Guid ipGuid)
        {
            return DatabaseManager.GetCommentsForGuid(ipGuid);
        }
        
        // Asynchronous log service method.
        public async Task StartFirewallServiceAsync(CancellationToken token)
        {
            // Reload firewall rules
            ReloadFirewallRules();

            var commandPort = 53860;
            
            var tasks = ActivateFirewallTasks();

            var pluginPath = Path.Combine(Environment.CurrentDirectory, "Plugins");
            
            if (_config.FirewallSettings.PluginsEnabled)
                PluginManager.LoadPlugins(pluginPath);

            // Startup summary banner
            // Prepare an indented bullet list for background firewall tasks
            var tasksDisplay = "     – " + string.Join("\n     – ", tasks);
            
            var now = DateTime.UtcNow;
            var initLines = new List<string>
            {
                "🔥 Firewall Service Summary 🔥",
                "",
                $"Timestamp              : {now:yyyy-MM-dd HH:mm:ss} UTC",
                $"Syslog path            : {GetSysLogPath}",
                $".NET runtime           : {Environment.Version}",
                $"Plugins directory      : {pluginPath}",
                $"Plugins loaded         : {PluginManager.Plugins.Count}",
                $"Whitelisted IPs        : {BlockListManager.WhitelistedIPs.Count}",
                $"Currently blocked IPs  : {BlockListManager.BlockedIPs.Count}",
                $"Thresholds             : {ThresholdAttempts} attempts in {ThresholdSeconds}s",
                $"Default block duration : {DefaultBlockDurationSeconds}s",
                $"Command server port    : {commandPort}",
                $"Console handler        : {(Console.IsInputRedirected ? "disabled" : "enabled")}",
                "Background tasks:"
            };
            
            initLines.AddRange(
                tasksDisplay
                    .Split(new[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => "• " + line)
            );

            LogRaw(MessageUtiliity.BuildAsciiBox(initLines));

            LogAction("Connection monitor started.");
            LogAction("Press Ctrl+C to stop.");
            
            // Start monitoring and command server
            List<Task> firewallTasks = new()
            {
                SyslogMonitor.StartMonitoring(token),
                StartCommandServerAsync(token, commandPort)
            };

            var consoleTask = Task.Run(async () => {
                while (!token.IsCancellationRequested)
                {
                    Console.Write("> ");
                    
                    var readLineTask = Task.Run(() => Console.ReadLine());
                    var finished = await Task.WhenAny(readLineTask, Task.Delay(-1, token));
                    if (finished != readLineTask)
                        break;

                    var line = readLineTask.Result;
                    if (line is null) break;
                    
                    if (!_config.FirewallSettings.AllowPlainTextCommands)
                    {
                        LogAction("Plain text commands are disabled.", LogLevel.WARNING);
                        continue;
                    }

                    CommandManager.ProcessCommand(line, this, out var resp);
                    if (!string.IsNullOrEmpty(resp))
                        LogAction(resp);
                }
            }, token);
            
            firewallTasks.Add(consoleTask);

            await Task.WhenAny(firewallTasks);
            Shutdown();
        }

        private string[] ActivateFirewallTasks()
        {
            var enabledTaskNames = _config.FirewallSettings.FirewallTasks.Tasks
                .Where(t => t.Enabled).Select(t => t.Name).ToArray();

            foreach (var name in enabledTaskNames)
            {
                switch (name)
                {
                    case nameof(ExpiredBlockCleanupTask):
                        new ExpiredBlockCleanupTask();
                        break;
                    
                    case nameof(PortScanDetectionTask):
                        new PortScanDetectionTask();
                        break;
                    
                    case nameof(CertExpiryMonitorTask):
                        new CertExpiryMonitorTask(
                            _config.FirewallSettings.Certificate.Path, 
                            TimeSpan.FromDays(5));
                        break;
                    
                    case nameof(HttpBruteForceMonitorTask):
                        new HttpBruteForceMonitorTask(
                            "/var/log/access.log", 
                            50, 
                            TimeSpan.FromMinutes(1));
                        break;
                    
                    case nameof(BandwidthMonitorTask):
                        new BandwidthMonitorTask(
                            "etho0", 
                            1_000_000);
                        break;
                }
            }
            
            return enabledTaskNames;
        }
        
        /// <summary>
        /// Gracefully stops background tasks and services.
        /// </summary>
        public void Shutdown()
        {
            LogAction("Shutting down runtime manager and tasks...", LogLevel.INFO);
            RuntimeManager.Stop();
            
            if (_config.FirewallSettings.PluginsEnabled)
                PluginManager.UnloadPlugins();
        }

        public void Encrypt(string content)
        {
            _crypto.Encrypt(content);
        }

        public void Decrypt(string content)
        {
            _crypto.Decrypt(content);
        }
        
        public readonly CryptoService _crypto;
        
        public static int LogRotationLineCount => _config.Logging.LogRotationLineCount;
        public static int MaxLogArchives => _config.Logging.MaxLogArchives;
        public static int ThresholdAttempts => _config.FirewallSettings.ThresholdAttempts;
        public static int ThresholdSeconds => _config.FirewallSettings.ThresholdSeconds;
        public static int DefaultBlockDurationSeconds => _config.FirewallSettings.DefaultBlockDurationSeconds;
        public ILogger GetLogger => Logger;
        
        public void RotateLogs()
        {
            LogManager.RotateLogs();
        }

        public void UnblockIP(string ip, Action<string, LogLevel> logAction)
        {
            IptablesManager.UnblockIP(ip, LogAction);
        }

        public void UnblockAll()
        {
            foreach (var ip in BlockListManager.BlockedIPs.ToList())
            {
                IptablesManager.UnblockIP(ip, LogAction);
            }
        }
        
        ICommandManager IFirewallContext.CommandManager 
            => this.CommandManager;
        
        IBlockListManager IFirewallContext.BlockListManager 
            => this.BlockListManager;

        void IFirewallContext.LogAction(string msg, LogLevel lvl) 
            => this.LogAction(msg, lvl);

        void IFirewallContext.ExportLogs(string f) 
            => this.ExportLogs(f);
        
        string IFirewallContext.ReadExportedLogs(string f)
            => this.ReadExportedLogs(f);

        void IFirewallContext.ManualBlockIP(string ip, int? d) 
            => this.ManualBlockIP(ip, d);

        void IFirewallContext.ReloadFirewallRules() 
            => this.ReloadFirewallRules();

        bool IFirewallContext.IsWhitelisted(string ip) 
            => Whitelist.Contains(ip);

        string IFirewallContext.GetBlockedAddressesResponse() 
            => this.GetBlockedAddressesResponse();

        string IFirewallContext.GetStatusResponse() 
            => this.GetStatusResponse();
        
        

    }
}