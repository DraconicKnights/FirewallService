using System.Diagnostics;
using FirewallInterface.Interface;

namespace FirewallCore.Utils
{
    public class MonitoringService
    {
        private readonly IFirewallContext _ctx;
        private CancellationTokenSource _cts;
        private CancellationTokenSource _inputCts;
        private ConsoleKey?       _lastKey;
        private Task _loop;
        private TimeSpan _lastCpu;
        private DateTime _lastSample;
        private readonly ISchedulerService _scheduler;
        private Guid                     _renderJobId;

        // New toggle flags
        private bool _showDetails;
        private bool _showHelp;
        private bool _sortByFails = true;
        private bool _filterBlocked;
        
        private bool _showAll;                   
        private int  _allSelectedIndex;
        private int  _allScrollOffset;

        // Track how many lines were printed last frame
        private int _lastLinesPrinted;
        public MonitoringService(IFirewallContext ctx, ISchedulerService scheduler)
        {
            _ctx        = ctx;
            _lastCpu    = Process.GetCurrentProcess().TotalProcessorTime;
            _scheduler  = scheduler;
            _lastSample = DateTime.UtcNow;
        }

        public void Start(int refreshMs = 500)
        {
            Console.CursorVisible = false;
            Console.Clear();

            // 1) key‐reader
            _inputCts = new CancellationTokenSource();
            Task.Run(() =>
            {
                while (!_inputCts.Token.IsCancellationRequested)
                {
                    var key = Console.ReadKey(true).Key;
                    HandleKey(key);

                    RenderOnce();
                }
            }, _inputCts.Token);

            // 2) schedule recurring redraw
            _renderJobId = _scheduler.ScheduleRecurring(
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(refreshMs),
                () =>
                {
                    DrainKey();
                    RenderOnce();
                }
            );
        }

        public void Stop()
        {
            _inputCts.Cancel();
            _scheduler.Cancel(_renderJobId);
            Console.CursorVisible = true;
            Console.ResetColor();
            Console.Clear();
        }
        
        private void HandleKey(ConsoleKey key)
        {
            switch (key)
            {
                case ConsoleKey.Q:
                    _cts.Cancel();
                    break;
                case ConsoleKey.D:
                    _showDetails = !_showDetails;
                    break;
                case ConsoleKey.H:
                    _showHelp = !_showHelp;
                    break;
                case ConsoleKey.S:
                    _sortByFails = !_sortByFails;
                    break;
                case ConsoleKey.B:
                    _filterBlocked = !_filterBlocked;
                    break;
                case ConsoleKey.R:
                    _ctx.ReloadFirewallRules();
                    break;
                case ConsoleKey.C:
                    GC.Collect();
                    break;
                case ConsoleKey.A:
                    // toggle full‐screen mode
                    // Need to fix still
                    if (_showAll)
                        _showAll = false;
                    else
                    {
                        _showAll = true;
                        _allSelectedIndex = _allScrollOffset = 0;
                    }
                    break;
                case ConsoleKey.UpArrow:
                    if (_showAll) _allSelectedIndex--;
                    break;
                case ConsoleKey.DownArrow:
                    if (_showAll) _allSelectedIndex++;
                    break;
            }
        }
        
        private bool DrainKey()
        {
            var key = _lastKey;
            _lastKey = null;
            if (key.HasValue)
            {
                HandleKey(key.Value);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Delay for up to <paramref name="delayMs"/> but poll for keypresses
        /// in small chunks so we stay responsive.
        /// </summary>
        private async Task WaitWithInputAsync(int delayMs, CancellationToken token)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < delayMs && !token.IsCancellationRequested)
            {
                if (DrainKey())
                    return;
                
                var remaining = delayMs - (int)sw.ElapsedMilliseconds;
                await Task.Delay(Math.Min(50, remaining), token)
                    .ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnRanToCompletion);
            }
        }
        
        private int RenderOnce()
        {
            // instead of Console.Clear() we do this to prevent stuttering issues
            Console.SetCursorPosition(0, 0);
            int width = Console.WindowWidth;
            for (int r = 0; r < _lastLinesPrinted; r++)
            {
                Console.SetCursorPosition(0, r);
                Console.Write(new string(' ', width));
            }
            Console.SetCursorPosition(0, 0);

            var now     = DateTime.Now;
            var proc    = Process.GetCurrentProcess();
            var uptime  = now - proc.StartTime;
            var threads = proc.Threads.Count;
            var gens    = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };

            // CPU Data
            var curCpu = proc.TotalProcessorTime;
            var dtMs   = (DateTime.UtcNow - _lastSample).TotalMilliseconds;
            var used   = (curCpu - _lastCpu).TotalMilliseconds;
            var cpuPct = dtMs > 0
                         ? Math.Min(100, (used / dtMs) / Environment.ProcessorCount * 100)
                         : 0;
            _lastCpu    = curCpu;
            _lastSample = DateTime.UtcNow;

            // MEM Data
            var memMb   = proc.WorkingSet64 / 1024.0 / 1024.0;
            var totalMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024.0 / 1024.0;
            var memPct  = totalMb > 0 ? Math.Min(100, memMb / totalMb * 100) : 0;

            // Firewall raw status
            var ipAttemptsDict = FirewallServiceProvider.IpAttempts;
            var blockList       = _ctx.BlockListManager.BlockedIPs;
            int totalAttempts   = ipAttemptsDict.Values.Sum(list => list.Count);

            // ============= HEADER ============
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            Console.ForegroundColor = ConsoleColor.White;
            string title = " 🔥 FIREWALL SERVICE MONITOR 🔥 ";
            string stamp = now.ToString("yyyy-MM-dd HH:mm:ss");
            string os    = $"{Environment.OSVersion.Platform} {Environment.OSVersion.Version}";
            string hdr   = title
                         + new string(' ', width - title.Length - stamp.Length - os.Length - 2)
                         + os + " " + stamp;
            Console.WriteLine(hdr.PadRight(width));
            Console.ResetColor();

            // ============= KEYBINDS ============
            Console.BackgroundColor = ConsoleColor.DarkGray;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.Write(" Q=Quit ");
            Console.Write(" D=Details ");
            Console.Write(" H=Help ");
            Console.Write(" S=Sort ");
            Console.Write(" B=BlockedOnly ");
            Console.Write(" R=Reload ");
            Console.Write(" C=GC ");
            Console.WriteLine(new string(' ', width - 60));
            Console.ResetColor();
            Console.WriteLine();

            // ============= BARS ============
            var separator = new string('─', width);
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(separator);
            Console.ResetColor();

            void DrawBar(string label, double pct, ConsoleColor fg)
            {
                int barW = width - label.Length - 12;
                int filled = (int)(barW * pct / 100);

                Console.BackgroundColor = ConsoleColor.DarkBlue;
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($" {label.PadRight(6)} ");
                Console.ResetColor();

                Console.Write(" [");
                for (int i = 0; i < barW; i++)
                {
                    Console.BackgroundColor = i < filled ? fg : ConsoleColor.DarkGray;
                    Console.Write(" ");
                }
                Console.ResetColor();
                Console.Write($"] ");
                Console.ForegroundColor = fg;
                Console.WriteLine($"{pct,6:0.0}%");
                Console.ResetColor();
            }

            DrawBar("CPU", cpuPct, cpuPct < 60 ? ConsoleColor.Green
                : cpuPct < 85 ? ConsoleColor.Yellow
                : ConsoleColor.Red);

            DrawBar("MEM", memPct, memPct < 60 ? ConsoleColor.Green
                : memPct < 85 ? ConsoleColor.Yellow
                : ConsoleColor.Red);

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(separator);
            Console.ResetColor();
            Console.WriteLine();

            // ============= PANELS ============
            int col = (width - 3) / 2;

            Console.BackgroundColor = ConsoleColor.DarkGreen;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.Write(" PROCESS ".PadRight(col, '='));
            Console.ResetColor();
            Console.Write(" | ");
            Console.BackgroundColor = ConsoleColor.DarkRed;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.Write(" FIREWALL ".PadRight(col, '='));
            Console.ResetColor();
            Console.WriteLine("\n");

            Console.Write($" PID     : {proc.Id}".PadRight(col)); Console.Write(" | "); Console.WriteLine();
            Console.Write($" Threads : {threads}".PadRight(col));  Console.Write(" | ");
            Console.Write($" BLOCKED  : {blockList.Count}".PadRight(col)); Console.WriteLine();
            Console.Write($" Uptime  : {uptime:dd\\:hh\\:mm\\:ss}".PadRight(col)); Console.Write(" | ");
            Console.Write($" ATTEMPTS : {totalAttempts}".PadRight(col)); Console.WriteLine();
            Console.Write($" GC0/1/2 : {gens[0]}/{gens[1]}/{gens[2]}".PadRight(col)); Console.Write(" | ");
            Console.Write($" MEM MB   : {memMb,6:0.0}".PadRight(col)); Console.WriteLine("\n");

            if (_showHelp)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("  HELP MENU:");
                Console.ResetColor();
                Console.WriteLine("    D: Toggle Details Panel");
                Console.WriteLine("    H: Toggle This Help");
                Console.WriteLine("    S: Sort by Fails / Time");
                Console.WriteLine("    B: Show Only Blocked IPs");
                Console.WriteLine("    R: Reload Firewall Rules");
                Console.WriteLine("    C: Trigger GC");
                Console.WriteLine("    Q: Quit");
                Console.WriteLine();
            }
            else if (_showAll)
            {
                // top 20 addresses mode
                    var allStats = ipAttemptsDict
                        .Select(kvp =>
                        {
                            var ip = kvp.Key;
                            var total = kvp.Value.Count;
                            var guid = _ctx.GetIdentifierForIp(ip);
                            var (all, fails, lastSeen) = _ctx.GetStatsForGuid(guid);
                            return new { IP = ip, TotalAttempts = total, Fails = fails, LastSeen = lastSeen };
                        })
                        .OrderByDescending(x => _sortByFails ? x.Fails : x.LastSeen.Ticks)
                        .Take(20)
                        .ToList();

                    int winH = Console.WindowHeight;
                    int headerLines = 4;
                    int bodyLines = winH - headerLines - 1;
                    
                    if (_allSelectedIndex < 0) _allSelectedIndex = 0;
                    if (_allSelectedIndex >= allStats.Count) _allSelectedIndex = allStats.Count - 1;
                    if (_allSelectedIndex < _allScrollOffset) _allScrollOffset = _allSelectedIndex;
                    if (_allSelectedIndex >= _allScrollOffset + bodyLines)
                        _allScrollOffset = _allSelectedIndex - bodyLines + 1;

                    int winW = Console.WindowWidth;
                    for (int r = 0; r < winH; r++)
                    {
                        Console.SetCursorPosition(0, r);
                        Console.Write(new string(' ', winW));
                    }
                    Console.SetCursorPosition(0, 0);

                    Console.BackgroundColor = ConsoleColor.DarkCyan;
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.WriteLine($"  TOP {allStats.Count} IPs  ".PadRight(winW));
                    Console.ResetColor();
                    Console.WriteLine("{0,-16} {1,6} {2,6} {3,20}", "IP Address", "Fails", "Total", "Last Seen");

                    for (int i = 0; i < bodyLines && i + _allScrollOffset < allStats.Count; i++)
                    {
                        var s = allStats[i + _allScrollOffset];
                        bool isSel = (i + _allScrollOffset) == _allSelectedIndex;

                        // highlight selected
                        if (isSel)
                        {
                            Console.BackgroundColor = ConsoleColor.Cyan;
                            Console.ForegroundColor = ConsoleColor.Black;
                        }
                        else
                        {
                            Console.ResetColor();
                            Console.ForegroundColor = blockList.Contains(s.IP)
                                ? ConsoleColor.Red
                                : ConsoleColor.Green;
                        }

                        Console.Write($"{s.IP,-16}");
                        Console.ResetColor();
                        Console.Write($" {s.Fails,6} {s.TotalAttempts,6} {s.LastSeen,20}");
                        Console.WriteLine();
                    }

                    Console.WriteLine();
                    Console.BackgroundColor = ConsoleColor.DarkCyan;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(" ↑/↓ Scroll  A=Back ");
                    Console.WriteLine(new string(' ', width - 20));
                    Console.ResetColor();

                    // consume scroll keys immediately
                    if (Console.KeyAvailable)
                    {
                        var k2 = Console.ReadKey(true).Key;
                        if (k2 == ConsoleKey.UpArrow)    _allSelectedIndex--;
                        if (k2 == ConsoleKey.DownArrow)  _allSelectedIndex++;
                        if (k2 == ConsoleKey.A)          _showAll = false;
                    }
            }
            else if (_showDetails)
            {
                // Prepare stats
                var recentStats = ipAttemptsDict
                    .Select(kvp =>
                    {
                        var ip = kvp.Key;
                        var total = kvp.Value.Count;
                        var guid = _ctx.GetIdentifierForIp(ip);
                        var (all, fails, lastSeen) = _ctx.GetStatsForGuid(guid);
                        return new { IP = ip, TotalAttempts = total, Fails = fails, LastSeen = lastSeen };
                    })
                    .Where(x => !_filterBlocked || blockList.Contains(x.IP))
                    .OrderByDescending(x => _sortByFails ? x.Fails : x.LastSeen.Ticks)
                    .Take(10)
                    .ToList();


                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("  Recent IP Stats:");
                Console.ResetColor();
                Console.WriteLine("{0,-16} {1,6} {2,6} {3,20}", "IP Address", "Fails", "Total", "Last Seen");
                foreach (var s in recentStats)
                {
                    Console.ForegroundColor = blockList.Contains(s.IP)
                        ? ConsoleColor.Red 
                        : ConsoleColor.Green;
                    Console.Write($"{s.IP,-16}");
                    Console.ResetColor();
                    Console.Write($" {s.Fails,6} {s.TotalAttempts,6} {s.LastSeen,20}");
                    Console.WriteLine();
                }
                Console.WriteLine();
            }

            // ============= FOOTER ============
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            Console.ForegroundColor = ConsoleColor.White;
            string mode = $"[D:{_showDetails}] [H:{_showHelp}] [S:{(_sortByFails ? "Fails" : "Time")}] [B:{(_filterBlocked ? "On" : "Off")}]";
            string footer = mode + new string(' ', width - mode.Length - 9) + now.ToString("HH:mm:ss");
            Console.WriteLine(footer.PadRight(width));
            Console.ResetColor();
            
            _lastLinesPrinted = Console.CursorTop;
            return _lastLinesPrinted;

        }
    }
}