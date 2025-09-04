namespace FirewallCore.Data;

public class BlockedAddress
{
    public string IP { get; set; }
    public DateTime BlockedTime { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime ScheduledUnblockTime => BlockedTime.AddSeconds(DurationSeconds);

    public BlockedAddress(string ip, DateTime blockedTime, int durationSeconds)
    {
        IP = ip;
        BlockedTime = blockedTime;
        DurationSeconds = durationSeconds;
    }

}