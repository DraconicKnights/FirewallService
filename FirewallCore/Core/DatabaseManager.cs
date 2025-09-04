using FirewallCore.Data;
using Microsoft.Data.Sqlite;

namespace FirewallCore.Core
{
    internal class DatabaseManager
    {
        private readonly string _connectionString;
        private readonly string _databaseFolder = "Database";

        public DatabaseManager()
        {
            // Ensure the designated folder exists.
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), _databaseFolder);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Define the database file path.
            string dbFile = Path.Combine(folderPath, "firewall.db");
            _connectionString = $"Data Source={dbFile}";

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS BlockedIPs (
                    IP TEXT PRIMARY KEY,
                    BlockedTime TEXT NOT NULL,
                    DurationSeconds INTEGER NOT NULL,
                    ScheduledUnblockTime TEXT NOT NULL
                );
            ";
            command.ExecuteNonQuery();
            
            // new history table
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS IpHistory (
                    IpGuid TEXT NOT NULL,
                    EventTime TEXT NOT NULL,
                    Message TEXT NOT NULL
                );
            ";
            command.ExecuteNonQuery();
            
            // existing tables...
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS IpTags (
                    IpGuid TEXT NOT NULL,
                    Tag TEXT NOT NULL,
                    UNIQUE(IpGuid, Tag)
                );
            ";
            command.ExecuteNonQuery();
            
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS IpComments (
                    IpGuid TEXT NOT NULL,
                    CommentTime TEXT NOT NULL,
                    Comment TEXT NOT NULL
                );
            ";
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Inserts or updates a blocked IP record.
        /// </summary>
        public void InsertBlockedIP(BlockedAddress blockedAddress)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO BlockedIPs (IP, BlockedTime, DurationSeconds, ScheduledUnblockTime)
                VALUES ($ip, $blockedTime, $durationSeconds, $scheduledUnblockTime);
            ";
            command.Parameters.AddWithValue("$ip", blockedAddress.IP);
            command.Parameters.AddWithValue("$blockedTime", blockedAddress.BlockedTime.ToString("o")); 
            command.Parameters.AddWithValue("$durationSeconds", blockedAddress.DurationSeconds);
            command.Parameters.AddWithValue("$scheduledUnblockTime", blockedAddress.ScheduledUnblockTime.ToString("o"));
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Deletes a blocked IP record.
        /// </summary>
        public void DeleteBlockedIP(string ip)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM BlockedIPs WHERE IP = $ip;";
            command.Parameters.AddWithValue("$ip", ip);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Retrieves all blocked IPs from the database.
        /// </summary>
        public List<BlockedAddress> GetBlockedIPs()
        {
            var blockedAddresses = new List<BlockedAddress>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT IP, BlockedTime, DurationSeconds FROM BlockedIPs;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string ip = reader.GetString(0);
                DateTime blockedTime = DateTime.Parse(reader.GetString(1));
                int durationSeconds = reader.GetInt32(2);
                blockedAddresses.Add((new BlockedAddress(ip, blockedTime, durationSeconds)));
            }
            return blockedAddresses;
        }

        /// <summary>
        /// Checks for expired block entries and removes them.
        /// Entry is considered expired if the current UTC time is beyond (BlockedTime + DurationSeconds).
        /// </summary>
        public List<BlockedAddress> RemoveExpiredBlockedIPs()
        {
            var now = DateTime.UtcNow;

            // 1) Load all blocked rows
            var all = GetBlockedIPs();

            // 2) Filter out the expired ones
            var expired = all
                .Where(b => b.BlockedTime.AddSeconds(b.DurationSeconds) <= now)
                .ToList();

            foreach (var b in expired)
            {
                DeleteBlockedIP(b.IP);
            }

            // 4) Return them for further in-memory cleanup
            return expired;
        }
        
        /// <summary>
        /// Retrieves the full event history for a given IP GUID.
        /// </summary>
        public IEnumerable<(DateTime Time, string Message)> GetHistoryForGuid(Guid ipGuid)
        {
            var list = new List<(DateTime, string)>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT EventTime, Message
                  FROM IpHistory
                 WHERE IpGuid = $g
              ORDER BY EventTime ASC;
            ";
            cmd.Parameters.AddWithValue("$g", ipGuid.ToString());
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var time = DateTime.Parse(reader.GetString(0));
                var msg  = reader.GetString(1);
                list.Add((time, msg));
            }
            return list;
        }

        /// <summary>
        /// Deletes all history entries for a given IP GUID.
        /// </summary>
        public void ClearHistory(Guid ipGuid)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM IpHistory WHERE IpGuid = $g;";
            cmd.Parameters.AddWithValue("$g", ipGuid.ToString());
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Computes simple stats for the IP GUID: total events, failures, and last seen timestamp.
        /// </summary>
        public (int totalAttempts, int recentFails, DateTime lastSeen) GetStatsForGuid(Guid ipGuid)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            // total event count
            cmd.CommandText = "SELECT COUNT(*) FROM IpHistory WHERE IpGuid = $g;";
            cmd.Parameters.AddWithValue("$g", ipGuid.ToString());
            int total = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

            // count of events whose message contains "fail" (case‐insensitive)
            cmd.CommandText = @"
                SELECT COUNT(*) 
                  FROM IpHistory 
                 WHERE IpGuid = $g
                   AND LOWER(Message) LIKE '%fail%';
            ";
            int fails = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

            // most recent event time
            cmd.CommandText = @"
                SELECT MAX(EventTime) 
                  FROM IpHistory 
                 WHERE IpGuid = $g;
            ";
            var maxTime = cmd.ExecuteScalar() as string;
            DateTime lastSeen = maxTime != null
                ? DateTime.Parse(maxTime)
                : DateTime.MinValue;

            return (total, fails, lastSeen);
        }

        /// <summary>
        /// Appends a new history record for an IP GUID.
        /// </summary>
        public void InsertHistory(Guid ipGuid, DateTime timestamp, string message)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO IpHistory (IpGuid, EventTime, Message)
                VALUES ($g, $t, $m);
            ";
            cmd.Parameters.AddWithValue("$g", ipGuid.ToString());
            cmd.Parameters.AddWithValue("$t", timestamp.ToString("o"));
            cmd.Parameters.AddWithValue("$m", message);
            cmd.ExecuteNonQuery();
        }
        
        /// <summary>
        /// Associates a tag with an IP GUID.
        /// </summary>
        public void InsertTag(Guid ipGuid, string tag)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
        INSERT OR IGNORE INTO IpTags (IpGuid, Tag)
        VALUES ($g, $t);
    ";
            cmd.Parameters.AddWithValue("$g", ipGuid.ToString());
            cmd.Parameters.AddWithValue("$t", tag);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Removes a tag association from an IP GUID.
        /// </summary>
        public void DeleteTag(Guid ipGuid, string tag)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
        DELETE FROM IpTags
         WHERE IpGuid = $g
           AND Tag = $t;
    ";
            cmd.Parameters.AddWithValue("$g", ipGuid.ToString());
            cmd.Parameters.AddWithValue("$t", tag);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Retrieves all tags for an IP GUID.
        /// </summary>
        public IEnumerable<string> GetTagsForGuid(Guid ipGuid)
        {
            var tags = new List<string>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
        SELECT Tag
          FROM IpTags
         WHERE IpGuid = $g;
    ";
            cmd.Parameters.AddWithValue("$g", ipGuid.ToString());
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                tags.Add(reader.GetString(0));
            }
            return tags;
        }
        
        /// <summary>
        /// Inserts a new comment for an IP GUID, timestamped now.
        /// </summary>
        public void InsertComment(Guid ipGuid, string comment)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO IpComments (IpGuid, CommentTime, Comment)
                VALUES ($g, $t, $c);
            ";
            cmd.Parameters.AddWithValue("$g", ipGuid.ToString());
            cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("$c", comment);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Retrieves all comments for an IP GUID, in chronological order.
        /// </summary>
        public IEnumerable<(DateTime Time, string Comment)> GetCommentsForGuid(Guid ipGuid)
        {
            var list = new List<(DateTime, string)>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT CommentTime, Comment
                  FROM IpComments
                 WHERE IpGuid = $g
              ORDER BY CommentTime ASC;
            ";
            cmd.Parameters.AddWithValue("$g", ipGuid.ToString());
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var time = DateTime.Parse(reader.GetString(0));
                var text = reader.GetString(1);
                list.Add((time, text));
            }
            return list;
        }
    }
}