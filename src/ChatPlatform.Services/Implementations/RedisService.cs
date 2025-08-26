using ChatPlatform.Core.Common;
using ChatPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

using SERedis = StackExchange.Redis;

namespace ChatPlatform.Services.Implementations;

public class RedisService : IRedisService, IDisposable
{
    private readonly SERedis.IConnectionMultiplexer _connection;
    private readonly ILogger<RedisService> _logger;
    private readonly RedisConfig _config;
    private SERedis.ISubscriber? _subscriber;
    private readonly Dictionary<string, Action<string, string>> _handlers = new();

    public RedisService(SERedis.IConnectionMultiplexer connection, ILogger<RedisService> logger, IOptions<RedisConfig> config)
    {
        _connection = connection;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<bool> PublishAsync(string channel, object message)
    {
        try
        {
            var subscriber = GetSubscriber();
            var serializedMessage = System.Text.Json.JsonSerializer.Serialize(message);
            var result = await subscriber.PublishAsync(channel, serializedMessage);
            _logger.LogDebug("Published message to channel {Channel}, result: {Result}", channel, result);
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message to channel {Channel}", channel);
            return false;
        }
    }

    public async Task<long> PublishAsync(string channel, string message)
    {
        try
        {
            var subscriber = GetSubscriber();
            var result = await subscriber.PublishAsync(channel, message);
            _logger.LogDebug("Published string message to channel {Channel}, result: {Result}", channel, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing string message to channel {Channel}", channel);
            return 0;
        }
    }

    public async Task SubscribeAsync(string channel, Action<string, string> handler)
    {
        try
        {
            var subscriber = GetSubscriber();
            _handlers[channel] = handler;
            
            await subscriber.SubscribeAsync(channel, (_, value) =>
            {
                if (_handlers.TryGetValue(channel, out var handler))
                {
                    handler(channel, value);
                }
            });
            
            _logger.LogInformation("Subscribed to channel {Channel}", channel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to channel {Channel}", channel);
            throw;
        }
    }

    public async Task UnsubscribeAsync(string channel)
    {
        try
        {
            var subscriber = GetSubscriber();
            await subscriber.UnsubscribeAsync(channel);
            _handlers.Remove(channel);
            _logger.LogInformation("Unsubscribed from channel {Channel}", channel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing from channel {Channel}", channel);
            throw;
        }
    }

    public async Task<string> AddToStreamAsync(string streamKey, Dictionary<string, string> fields)
    {
        try
        {
            var db = _connection.GetDatabase();
            var entryId = await db.StreamAddAsync(streamKey, fields.Select(f => new NameValueEntry(f.Key, f.Value)).ToArray());
            _logger.LogDebug("Added entry {EntryId} to stream {StreamKey}", entryId, streamKey);
            return entryId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding entry to stream {StreamKey}", streamKey);
            throw;
        }
    }

    public async Task<List<object>> ReadStreamAsync(string streamKey, string? position = null, int count = 100)
    {
        try
        {
            var db = _connection.GetDatabase();
            var positionToUse = position ?? "0-0";
            var entries = await db.StreamReadAsync(streamKey, positionToUse, count);
            _logger.LogDebug("Read {Count} entries from stream {StreamKey} from position {Position}", entries.Length, streamKey, positionToUse);
            return entries.Cast<object>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading stream {StreamKey} from position {Position}", streamKey, position);
            throw;
        }
    }

    public async Task<long> StreamLengthAsync(string streamKey)
    {
        try
        {
            var db = _connection.GetDatabase();
            var length = await db.StreamLengthAsync(streamKey);
            _logger.LogDebug("Stream {StreamKey} length: {Length}", streamKey, length);
            return length;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stream length for {StreamKey}", streamKey);
            throw;
        }
    }

    public async Task<bool> TrimStreamAsync(string streamKey, int maxLength)
    {
        try
        {
            var db = _connection.GetDatabase();
            var trimmed = await db.StreamTrimAsync(streamKey, maxLength);
            _logger.LogDebug("Trimmed stream {StreamKey} to max length {MaxLength}, removed {Trimmed} entries", streamKey, maxLength, trimmed);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error trimming stream {StreamKey} to max length {MaxLength}", streamKey, maxLength);
            return false;
        }
    }

    public async Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.StringSetAsync(key, value, expiry);
            _logger.LogDebug("Set key {Key} with expiry {Expiry}, result: {Result}", key, expiry, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting key {Key}", key);
            return false;
        }
    }

    public async Task<bool> SetAsync(string key, object value, TimeSpan? expiry = null)
    {
        try
        {
            var db = _connection.GetDatabase();
            var serializedValue = System.Text.Json.JsonSerializer.Serialize(value);
            var result = await db.StringSetAsync(key, serializedValue, expiry);
            _logger.LogDebug("Set object key {Key} with expiry {Expiry}, result: {Result}", key, expiry, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting object key {Key}", key);
            return false;
        }
    }

    public async Task<string?> GetAsync(string key)
    {
        try
        {
            var db = _connection.GetDatabase();
            var value = await db.StringGetAsync(key);
            _logger.LogDebug("Got key {Key}, has value: {HasValue}", key, value.HasValue);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting key {Key}", key);
            return null;
        }
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var db = _connection.GetDatabase();
            var value = await db.StringGetAsync(key);
            if (!value.HasValue)
            {
                return default;
            }
            
            var result = System.Text.Json.JsonSerializer.Deserialize<T>(value);
            _logger.LogDebug("Got typed key {Key}, type: {Type}, has value: {HasValue}", key, typeof(T).Name, result != null);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting typed key {Key} for type {Type}", key, typeof(T).Name);
            return default;
        }
    }

    public async Task<bool> DeleteAsync(string key)
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.KeyDeleteAsync(key);
            _logger.LogDebug("Deleted key {Key}, result: {Result}", key, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting key {Key}", key);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.KeyExistsAsync(key);
            _logger.LogDebug("Key {Key} exists: {Result}", key, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if key {Key} exists", key);
            return false;
        }
    }

    public async Task<bool> ExpireAsync(string key, TimeSpan expiry)
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.KeyExpireAsync(key, expiry);
            _logger.LogDebug("Set expiry {Expiry} for key {Key}, result: {Result}", expiry, key, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting expiry {Expiry} for key {Key}", expiry, key);
            return false;
        }
    }

    public async Task<bool> HashSetAsync(string key, string field, string value)
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.HashSetAsync(key, field, value);
            _logger.LogDebug("Set hash field {Field} for key {Key}, result: {Result}", field, key, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting hash field {Field} for key {Key}", field, key);
            return false;
        }
    }

    public async Task<bool> HashSetAsync(string key, Dictionary<string, string> fields)
    {
        try
        {
            var db = _connection.GetDatabase();
            var entries = fields.Select(f => new HashEntry(f.Key, f.Value)).ToArray();
            await db.HashSetAsync(key, entries);
            _logger.LogDebug("Set {Count} hash fields for key {Key}", fields.Count, key);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting hash fields for key {Key}", key);
            return false;
        }
    }

    public async Task<string?> HashGetAsync(string key, string field)
    {
        try
        {
            var db = _connection.GetDatabase();
            var value = await db.HashGetAsync(key, field);
            _logger.LogDebug("Got hash field {Field} for key {Key}, has value: {HasValue}", field, key, value.HasValue);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hash field {Field} for key {Key}", field, key);
            return null;
        }
    }

    public async Task<Dictionary<string, string>> HashGetAllAsync(string key)
    {
        try
        {
            var db = _connection.GetDatabase();
            var entries = await db.HashGetAllAsync(key);
            var result = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
            _logger.LogDebug("Got all hash fields for key {Key}, count: {Count}", key, result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all hash fields for key {Key}", key);
            return new Dictionary<string, string>();
        }
    }

    public async Task<bool> HashDeleteAsync(string key, string field)
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.HashDeleteAsync(key, field);
            _logger.LogDebug("Deleted hash field {Field} for key {Key}, result: {Result}", field, key, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting hash field {Field} for key {Key}", field, key);
            return false;
        }
    }

    public async Task<long> HashLengthAsync(string key)
    {
        try
        {
            var db = _connection.GetDatabase();
            var length = await db.HashLengthAsync(key);
            _logger.LogDebug("Hash key {Key} length: {Length}", key, length);
            return length;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hash length for key {Key}", key);
            return 0;
        }
    }

    public async Task<bool> SetAddAsync(string key, string member)
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.SetAddAsync(key, member);
            _logger.LogDebug("Added member {Member} to set {Key}, result: {Result}", member, key, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding member {Member} to set {Key}", member, key);
            return false;
        }
    }

    public async Task<bool> SetAddAsync(string key, IEnumerable<string> members)
    {
        try
        {
            var db = _connection.GetDatabase();
            var redisValues = members.Select(m => (RedisValue)m).ToArray();
            var result = await db.SetAddAsync(key, redisValues);
            _logger.LogDebug("Added {Count} members to set {Key}, result: {Result}", members.Count(), key, result);
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding members to set {Key}", key);
            return false;
        }
    }

    public async Task<bool> SetRemoveAsync(string key, string member)
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.SetRemoveAsync(key, member);
            _logger.LogDebug("Removed member {Member} from set {Key}, result: {Result}", member, key, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing member {Member} from set {Key}", member, key);
            return false;
        }
    }

    public async Task<bool> SetContainsAsync(string key, string member)
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.SetContainsAsync(key, member);
            _logger.LogDebug("Set {Key} contains member {Member}: {Result}", key, member, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if set {Key} contains member {Member}", key, member);
            return false;
        }
    }

    public async Task<string[]> SetMembersAsync(string key)
    {
        try
        {
            var db = _connection.GetDatabase();
            var members = await db.SetMembersAsync(key);
            var result = members.Select(m => m.ToString()).ToArray();
            _logger.LogDebug("Got {Count} members from set {Key}", result.Length, key);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting members from set {Key}", key);
            return Array.Empty<string>();
        }
    }

    public async Task<T[]> SetMembersAsync<T>(string key)
    {
        try
        {
            var db = _connection.GetDatabase();
            var members = await db.SetMembersAsync(key);
            var result = members.Select(m => System.Text.Json.JsonSerializer.Deserialize<T>(m.ToString())).Where(x => x != null).ToArray();
            _logger.LogDebug("Got {Count} typed members from set {Key}", result.Length, key);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting typed members from set {Key}", key);
            return Array.Empty<T>();
        }
    }

    public async Task<long> SetLengthAsync(string key)
    {
        try
        {
            var db = _connection.GetDatabase();
            var length = await db.SetLengthAsync(key);
            _logger.LogDebug("Set {Key} length: {Length}", key, length);
            return length;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting set length for {Key}", key);
            return 0;
        }
    }

    public async Task<bool> SortedSetAddAsync(string key, string member, double score)
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.SortedSetAddAsync(key, member, score);
            _logger.LogDebug("Added member {Member} with score {Score} to sorted set {Key}, result: {Result}", member, score, key, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding member {Member} with score {Score} to sorted set {Key}", member, score, key);
            return false;
        }
    }

    public async Task<bool> SortedSetAddAsync(string key, Dictionary<string, double> members)
    {
        try
        {
            var db = _connection.GetDatabase();
            var entries = members.Select(m => new SortedSetEntry(m.Key, m.Value)).ToArray();
            var result = await db.SortedSetAddAsync(key, entries);
            _logger.LogDebug("Added {Count} members to sorted set {Key}, result: {Result}", members.Count, key, result);
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding members to sorted set {Key}", key);
            return false;
        }
    }

    public async Task<double?> SortedSetScoreAsync(string key, string member)
    {
        try
        {
            var db = _connection.GetDatabase();
            var score = await db.SortedSetScoreAsync(key, member);
            _logger.LogDebug("Member {Member} score in sorted set {Key}: {Score}", member, key, score);
            return score;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting score for member {Member} in sorted set {Key}", member, key);
            return null;
        }
    }

    public async Task<string[]> SortedSetRangeByScoreAsync(string key, double min = double.MinValue, double max = double.MaxValue, int skip = 0, int take = -1)
    {
        try
        {
            var db = _connection.GetDatabase();
            var members = await db.SortedSetRangeByScoreAsync(key, min, max, Exclude.None, Order.Ascending, skip, take);
            var result = members.Select(m => m.ToString()).ToArray();
            _logger.LogDebug("Got {Count} members from sorted set {Key} by score range [{Min}, {Max}]", result.Length, key, min, max);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting members from sorted set {Key} by score range [{Min}, {Max}]", key, min, max);
            return Array.Empty<string>();
        }
    }

    public async Task<long> SortedSetLengthAsync(string key)
    {
        try
        {
            var db = _connection.GetDatabase();
            var length = await db.SortedSetLengthAsync(key);
            _logger.LogDebug("Sorted set {Key} length: {Length}", key, length);
            return length;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sorted set length for {Key}", key);
            return 0;
        }
    }

    public async Task<long> ListLeftPushAsync(string key, string value)
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.ListLeftPushAsync(key, value);
            _logger.LogDebug("Left pushed value to list {Key}, new length: {Result}", key, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error left pushing value to list {Key}", key);
            return 0;
        }
    }

    public async Task<long> ListRightPushAsync(string key, string value)
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.ListRightPushAsync(key, value);
            _logger.LogDebug("Right pushed value to list {Key}, new length: {Result}", key, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error right pushing value to list {Key}", key);
            return 0;
        }
    }

    public async Task<string?> ListLeftPopAsync(string key)
    {
        try
        {
            var db = _connection.GetDatabase();
            var value = await db.ListLeftPopAsync(key);
            _logger.LogDebug("Left popped value from list {Key}, has value: {HasValue}", key, value.HasValue);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error left popping value from list {Key}", key);
            return null;
        }
    }

    public async Task<string?> ListRightPopAsync(string key)
    {
        try
        {
            var db = _connection.GetDatabase();
            var value = await db.ListRightPopAsync(key);
            _logger.LogDebug("Right popped value from list {Key}, has value: {HasValue}", key, value.HasValue);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error right popping value from list {Key}", key);
            return null;
        }
    }

    public async Task<string[]> ListRangeAsync(string key, long start = 0, long stop = -1)
    {
        try
        {
            var db = _connection.GetDatabase();
            var values = await db.ListRangeAsync(key, start, stop);
            var result = values.Select(v => v.ToString()).ToArray();
            _logger.LogDebug("Got {Count} values from list {Key} range [{Start}, {Stop}]", result.Length, key, start, stop);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting values from list {Key} range [{Start}, {Stop}]", key, start, stop);
            return Array.Empty<string>();
        }
    }

    public async Task<T[]> ListRangeAsync<T>(string key, long start = 0, long stop = -1)
    {
        try
        {
            var db = _connection.GetDatabase();
            var values = await db.ListRangeAsync(key, start, stop);
            var result = values.Select(v => System.Text.Json.JsonSerializer.Deserialize<T>(v.ToString())).Where(x => x != null).ToArray();
            _logger.LogDebug("Got {Count} typed values from list {Key} range [{Start}, {Stop}]", result.Length, key, start, stop);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting typed values from list {Key} range [{Start}, {Stop}]", key, start, stop);
            return Array.Empty<T>();
        }
    }

    public async Task<long> ListLengthAsync(string key)
    {
        try
        {
            var db = _connection.GetDatabase();
            var length = await db.ListLengthAsync(key);
            _logger.LogDebug("List {Key} length: {Length}", key, length);
            return length;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting list length for {Key}", key);
            return 0;
        }
    }

    public async Task<bool> ListRemoveAsync(string key, string value)
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.ListRemoveAsync(key, value);
            _logger.LogDebug("Removed {Count} occurrences of value from list {Key}", result, key);
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing value from list {Key}", key);
            return false;
        }
    }

    public async Task<long> IncrementAsync(string key)
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.StringIncrementAsync(key);
            _logger.LogDebug("Incremented key {Key}, new value: {Result}", key, result);
            return (long)result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing key {Key}", key);
            return 0;
        }
    }

    public async Task<long> IncrementByAsync(string key, long value)
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.StringIncrementAsync(key, value);
            _logger.LogDebug("Incremented key {Key} by {Value}, new value: {Result}", key, value, result);
            return (long)result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing key {Key} by {Value}", key, value);
            return 0;
        }
    }

    public async Task<double> IncrementByAsync(string key, double value)
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.StringIncrementAsync(key, value);
            _logger.LogDebug("Incremented key {Key} by {Value}, new value: {Result}", key, value, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing key {Key} by {Value}", key, value);
            return 0;
        }
    }

    public async Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan? expiry = null)
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.StringSetAsync(key, value, expiry, When.NotExists);
            _logger.LogDebug("Set key {Key} if not exists with expiry {Expiry}, result: {Result}", key, expiry, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting key {Key} if not exists", key);
            return false;
        }
    }

    public async Task<Dictionary<string, string?>> GetMultipleAsync(params string[] keys)
    {
        try
        {
            var db = _connection.GetDatabase();
            var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
            var values = await db.StringGetAsync(redisKeys);
            var result = keys.Zip(values, (k, v) => new { Key = k, Value = v.HasValue ? v.ToString() : null })
                           .ToDictionary(x => x.Key, x => x.Value);
            _logger.LogDebug("Got multiple keys, count: {Count}", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting multiple keys");
            return new Dictionary<string, string?>();
        }
    }

    public async Task<bool> SetMultipleAsync(Dictionary<string, string> keyValues, TimeSpan? expiry = null)
    {
        try
        {
            var db = _connection.GetDatabase();
            var transaction = db.CreateTransaction();
            
            foreach (var kvp in keyValues)
            {
                transaction.StringSetAsync(kvp.Key, kvp.Value, expiry);
            }
            
            var result = await transaction.ExecuteAsync();
            _logger.LogDebug("Set multiple keys, count: {Count}, success: {Result}", keyValues.Count, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting multiple keys");
            return false;
        }
    }

    public async Task<long> DeleteMultipleAsync(params string[] keys)
    {
        try
        {
            var db = _connection.GetDatabase();
            var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
            var result = await db.KeyDeleteAsync(redisKeys);
            _logger.LogDebug("Deleted multiple keys, count: {Count}, deleted: {Result}", keys.Length, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting multiple keys");
            return 0;
        }
    }

    public async Task<string[]> GetKeysAsync(string pattern)
    {
        try
        {
            var server = _connection.GetServer(_connection.GetEndPoints().First());
            var keys = server.Keys(pattern: pattern);
            var result = keys.Select(k => k.ToString()).ToArray();
            _logger.LogDebug("Got keys matching pattern {Pattern}, count: {Count}", pattern, result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting keys matching pattern {Pattern}", pattern);
            return Array.Empty<string>();
        }
    }

    public async Task<RedisHealth> GetHealthAsync()
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var db = _connection.GetDatabase();
            var pingResult = await db.PingAsync();
            var responseTime = DateTime.UtcNow - startTime;

            var server = _connection.GetServer(_connection.GetEndPoints().First());
            var info = await server.InfoAsync();
            
            var connectedClients = info.FirstOrDefault(x => x.Key == "connected_clients")?.FirstOrDefault().Value;
            var usedMemory = info.FirstOrDefault(x => x.Key == "used_memory")?.FirstOrDefault().Value;
            var totalKeys = info.FirstOrDefault(x => x.Key == "db0")?.FirstOrDefault().Value;

            var health = new RedisHealth
            {
                IsHealthy = true,
                Status = "Healthy",
                ResponseTime = responseTime,
                ConnectedClients = int.TryParse(connectedClients, out var clients) ? clients : 0,
                UsedMemory = long.TryParse(usedMemory, out var memory) ? memory : 0,
                TotalKeys = long.TryParse(totalKeys, out var keys) ? keys : 0,
                CheckedAt = DateTime.UtcNow
            };

            _logger.LogDebug("Redis health check completed, response time: {ResponseTime}", responseTime);
            return health;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis health check failed");
            return new RedisHealth
            {
                IsHealthy = false,
                Status = "Unhealthy",
                ResponseTime = TimeSpan.Zero,
                ConnectedClients = 0,
                UsedMemory = 0,
                TotalKeys = 0,
                CheckedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<Dictionary<string, object>> GetInfoAsync()
    {
        try
        {
            var server = _connection.GetServer(_connection.GetEndPoints().First());
            var info = await server.InfoAsync();
            
            var result = new Dictionary<string, object>();
            foreach (var section in info)
            {
                foreach (var entry in section)
                {
                    result[$"{section.Key}:{entry.Key}"] = entry.Value;
                }
            }

            _logger.LogDebug("Got Redis info, sections: {Count}", info.Count());
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Redis info");
            return new Dictionary<string, object>();
        }
    }

    public async Task<bool> PingAsync()
    {
        try
        {
            var db = _connection.GetDatabase();
            var result = await db.PingAsync();
            _logger.LogDebug("Redis ping result: {Result}", result);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis ping failed");
            return false;
        }
    }

    private SERedis.ISubscriber GetSubscriber()
    {
        if (_subscriber == null)
        {
            _subscriber = _connection.GetSubscriber();
        }
        return _subscriber;
    }

    public void Dispose()
    {
        if (_subscriber != null)
        {
            _subscriber.UnsubscribeAll();
        }
        _connection?.Dispose();
    }
}
