namespace FirewallInterface.Interface;

public interface IBlockListManager
{
    string  WhitelistPath { get; }
    IReadOnlyCollection<string> BlockedIPs { get; }
    IReadOnlyCollection<string> WhitelistedIPs { get; }
    bool IsWhitelisted(string ip);
    bool IsBlocked(string ip);
    void Add(string ip);
    void Remove(string ip);
}