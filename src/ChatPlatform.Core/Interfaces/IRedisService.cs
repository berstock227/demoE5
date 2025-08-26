using ChatPlatform.Core.Models;

namespace ChatPlatform.Core.Interfaces;

public interface IRedisService
{
    // Pub/Sub Operations
    Task<bool> PublishAsync(string channel, object message);
    Task<long> PublishAsync(string channel, string message);
    Task SubscribeAsync(string channel, Action<string, string> handler);
    Task UnsubscribeAsync(string channel);
    
    // Stream Operations
    Task<string> AddToStreamAsync(string streamKey, Dictionary<string, string> fields);
    Task<List<object>> ReadStreamAsync(string streamKey, string? position = null, int count = 100);
    Task<long> StreamLengthAsync(string streamKey);
    Task<bool> TrimStreamAsync(string streamKey, int maxLength);
    
    // Key-Value Operations
    Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null);
    Task<bool> SetAsync(string key, object value, TimeSpan? expiry = null);
    Task<string?> GetAsync(string key);
    Task<T?> GetAsync<T>(string key);
    Task<bool> DeleteAsync(string key);
    Task<bool> ExistsAsync(string key);
    Task<bool> ExpireAsync(string key, TimeSpan expiry);
    
    // Hash Operations
    Task<bool> HashSetAsync(string key, string field, string value);
    Task<bool> HashSetAsync(string key, Dictionary<string, string> fields);
    Task<string?> HashGetAsync(string key, string field);
    Task<Dictionary<string, string>> HashGetAllAsync(string key);
    Task<bool> HashDeleteAsync(string key, string field);
    Task<long> HashLengthAsync(string key);
    
    // Set Operations
    Task<bool> SetAddAsync(string key, string member);
    Task<bool> SetAddAsync(string key, IEnumerable<string> members);
    Task<bool> SetRemoveAsync(string key, string member);
    Task<bool> SetContainsAsync(string key, string member);
    Task<string[]> SetMembersAsync(string key);
    Task<T[]> SetMembersAsync<T>(string key);
    Task<long> SetLengthAsync(string key);
    
    // Sorted Set Operations
    Task<bool> SortedSetAddAsync(string key, string member, double score);
    Task<bool> SortedSetAddAsync(string key, Dictionary<string, double> members);
    Task<double?> SortedSetScoreAsync(string key, string member);
    Task<string[]> SortedSetRangeByScoreAsync(string key, double min = double.MinValue, double max = double.MaxValue, int skip = 0, int take = -1);
    Task<long> SortedSetLengthAsync(string key);
    
    // List Operations
    Task<long> ListLeftPushAsync(string key, string value);
    Task<long> ListRightPushAsync(string key, string value);
    Task<string?> ListLeftPopAsync(string key);
    Task<string?> ListRightPopAsync(string key);
    Task<string[]> ListRangeAsync(string key, long start = 0, long stop = -1);
    Task<T[]> ListRangeAsync<T>(string key, long start = 0, long stop = -1);
    Task<long> ListLengthAsync(string key);
    Task<bool> ListRemoveAsync(string key, string value);
    
    // Atomic Operations
    Task<long> IncrementAsync(string key);
    Task<long> IncrementByAsync(string key, long value);
    Task<double> IncrementByAsync(string key, double value);
    Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan? expiry = null);
    
    // Batch Operations
    Task<Dictionary<string, string?>> GetMultipleAsync(params string[] keys);
    Task<bool> SetMultipleAsync(Dictionary<string, string> keyValues, TimeSpan? expiry = null);
    Task<long> DeleteMultipleAsync(params string[] keys);
    
    // Utility Operations
    Task<string[]> GetKeysAsync(string pattern);
    
    // Health & Monitoring
    Task<RedisHealth> GetHealthAsync();
    Task<Dictionary<string, object>> GetInfoAsync();
    Task<bool> PingAsync();
}

public class RedisHealth
{
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public TimeSpan ResponseTime { get; set; }
    public int ConnectedClients { get; set; }
    public long UsedMemory { get; set; }
    public long TotalKeys { get; set; }
    public DateTime CheckedAt { get; set; }
}

public class RedisConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public int Database { get; set; } = 0;
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan SyncTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public bool AbortConnect { get; set; } = false;
    public int ConnectRetry { get; set; } = 3;
    public bool EnableDetailedErrors { get; set; } = true;
}
