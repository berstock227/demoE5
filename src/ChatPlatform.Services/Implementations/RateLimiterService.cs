using ChatPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ChatPlatform.Services.Implementations;

public class RateLimiterService : IRateLimiter
{
    private readonly IRedisService _redisService;
    private readonly ILogger<RateLimiterService> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly RateLimiterConfig _config;
    private readonly ConcurrentDictionary<string, RateLimitConfig> _configs;

    public RateLimiterService(
        IRedisService redisService,
        ILogger<RateLimiterService> logger,
        IDateTimeProvider dateTimeProvider,
        IOptions<RateLimiterConfig> config)
    {
        _redisService = redisService ?? throw new ArgumentNullException(nameof(redisService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _configs = new ConcurrentDictionary<string, RateLimitConfig>();
        
        InitializeDefaultConfigs();
    }

    private void InitializeDefaultConfigs()
    {
        // Default rate limit configurations
        _configs.TryAdd("message", new RateLimitConfig
        {
            ResourceType = "message",
            Limit = _config.DefaultMessageLimit,
            Window = _config.DefaultWindow,
            BurstLimit = Math.Min(_config.DefaultMessageLimit / 10, 10),
            StrictMode = _config.StrictMode
        });

        _configs.TryAdd("typing", new RateLimitConfig
        {
            ResourceType = "typing",
            Limit = _config.DefaultTypingLimit,
            Window = _config.DefaultWindow,
            BurstLimit = Math.Min(_config.DefaultTypingLimit / 10, 5),
            StrictMode = _config.StrictMode
        });

        _configs.TryAdd("room_operations", new RateLimitConfig
        {
            ResourceType = "room_operations",
            Limit = _config.DefaultRoomOperationsLimit,
            Window = _config.DefaultWindow,
            BurstLimit = Math.Min(_config.DefaultRoomOperationsLimit / 10, 3),
            StrictMode = true
        });

        _configs.TryAdd("presence", new RateLimitConfig
        {
            ResourceType = "presence",
            Limit = _config.DefaultPresenceLimit,
            Window = _config.DefaultWindow,
            BurstLimit = Math.Min(_config.DefaultPresenceLimit / 10, 20),
            StrictMode = _config.StrictMode
        });
    }

    public async Task<bool> CheckLimitAsync(string key, string resourceType, int cost = 1)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(resourceType) || cost <= 0)
        {
            _logger.LogWarning("Invalid parameters for CheckLimitAsync: key={Key}, resourceType={ResourceType}, cost={Cost}", 
                key, resourceType, cost);
            return false;
        }

        try
        {
            var config = GetConfig(resourceType);
            var rateLimitKey = $"rate_limit:{resourceType}:{key}";
            
            // Get current usage
            var currentUsage = await GetCurrentUsageAsync(rateLimitKey, config);
            
            // Check if request is allowed
            if (currentUsage + cost <= config.Limit)
            {
                // Increment usage
                await IncrementUsageAsync(rateLimitKey, cost, config.Window);
                _logger.LogDebug("Rate limit check passed for {Key} on {ResourceType}, cost: {Cost}", key, resourceType, cost);
                return true;
            }

            _logger.LogWarning("Rate limit exceeded for {Key} on {ResourceType}, current: {Current}, limit: {Limit}, cost: {Cost}", 
                key, resourceType, currentUsage, config.Limit, cost);
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit for {Key} on {ResourceType}", key, resourceType);
            // In case of error, allow the request to prevent blocking legitimate users
            return true;
        }
    }

    public async Task<RateLimitInfo> GetRateLimitInfoAsync(string key, string resourceType)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(resourceType))
        {
            return new RateLimitInfo
            {
                Key = key ?? string.Empty,
                ResourceType = resourceType ?? string.Empty,
                Limit = 0,
                Remaining = 0,
                Used = 0,
                ResetTime = _dateTimeProvider.UtcNow,
                IsAllowed = false
            };
        }

        try
        {
            var config = GetConfig(resourceType);
            var rateLimitKey = $"rate_limit:{resourceType}:{key}";
            
            var currentUsage = await GetCurrentUsageAsync(rateLimitKey, config);
            var resetTime = await GetResetTimeAsync(rateLimitKey, config.Window);
            
            return new RateLimitInfo
            {
                Key = key,
                ResourceType = resourceType,
                Limit = config.Limit,
                Remaining = Math.Max(0, config.Limit - currentUsage),
                Used = currentUsage,
                ResetTime = resetTime,
                IsAllowed = currentUsage < config.Limit
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rate limit info for {Key} on {ResourceType}", key, resourceType);
            return new RateLimitInfo
            {
                Key = key,
                ResourceType = resourceType,
                Limit = 0,
                Remaining = 0,
                Used = 0,
                ResetTime = _dateTimeProvider.UtcNow,
                IsAllowed = false
            };
        }
    }

    public async Task<bool> ResetLimitAsync(string key, string resourceType)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(resourceType))
        {
            return false;
        }

        try
        {
            var rateLimitKey = $"rate_limit:{resourceType}:{key}";
            await _redisService.DeleteAsync(rateLimitKey);
            
            _logger.LogInformation("Rate limit reset for {Key} on {ResourceType}", key, resourceType);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting rate limit for {Key} on {ResourceType}", key, resourceType);
            return false;
        }
    }

    public async Task<bool> SetLimitAsync(string key, string resourceType, int limit, TimeSpan window)
    {
        if (string.IsNullOrEmpty(resourceType) || limit <= 0 || window <= TimeSpan.Zero)
        {
            _logger.LogWarning("Invalid parameters for SetLimitAsync: resourceType={ResourceType}, limit={Limit}, window={Window}", 
                resourceType, limit, window);
            return false;
        }

        try
        {
            var config = new RateLimitConfig
            {
                ResourceType = resourceType,
                Limit = limit,
                Window = window,
                BurstLimit = Math.Min(limit / 10, 5), // Default burst limit
                StrictMode = _config.StrictMode
            };

            _configs.AddOrUpdate(resourceType, config, (_, _) => config);
            
            _logger.LogInformation("Rate limit updated for {ResourceType}: limit={Limit}, window={Window}", resourceType, limit, window);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting rate limit for {ResourceType}", resourceType);
            return false;
        }
    }

    public async Task<Dictionary<string, bool>> CheckMultipleLimitsAsync(Dictionary<string, (string resourceType, int cost)> requests)
    {
        if (requests == null || requests.Count == 0)
        {
            return new Dictionary<string, bool>();
        }

        try
        {
            var results = new Dictionary<string, bool>();
            var tasks = new List<Task<(string key, bool result)>>();

            foreach (var request in requests)
            {
                if (!string.IsNullOrEmpty(request.Key) && !string.IsNullOrEmpty(request.Value.resourceType) && request.Value.cost > 0)
                {
                    tasks.Add(CheckLimitWithKeyAsync(request.Key, request.Value.resourceType, request.Value.cost));
                }
                else
                {
                    results[request.Key ?? string.Empty] = false;
                }
            }

            if (tasks.Count > 0)
            {
                var taskResults = await Task.WhenAll(tasks);
                foreach (var (key, result) in taskResults)
                {
                    results[key] = result;
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking multiple rate limits");
            // Return all false in case of error
            return requests.ToDictionary(r => r.Key, _ => false);
        }
    }

    public async Task<RateLimiterHealth> GetHealthAsync()
    {
        try
        {
            var redisHealth = await _redisService.GetHealthAsync();
            var totalKeys = _configs.Count;
            var activeKeys = _configs.Count; // All configs are considered active

            var keysByResourceType = _configs.ToDictionary(
                kvp => kvp.Key,
                kvp => 1 // Each resource type has one config
            );

            return new RateLimiterHealth
            {
                IsHealthy = redisHealth.IsHealthy,
                TotalKeys = totalKeys,
                ActiveKeys = activeKeys,
                KeysByResourceType = keysByResourceType,
                CheckedAt = _dateTimeProvider.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rate limiter health");
            return new RateLimiterHealth
            {
                IsHealthy = false,
                TotalKeys = 0,
                ActiveKeys = 0,
                KeysByResourceType = new Dictionary<string, int>(),
                CheckedAt = _dateTimeProvider.UtcNow
            };
        }
    }

    public async Task<Dictionary<string, RateLimitStats>> GetStatsAsync()
    {
        try
        {
            var stats = new Dictionary<string, RateLimitStats>();
            
            foreach (var config in _configs.Values)
            {
                var resourceType = config.ResourceType;
                
                // This is a simplified implementation - in production you'd want to track actual usage
                var statsData = new RateLimitStats
                {
                    ResourceType = resourceType,
                    TotalRequests = 0, // Would need to track this
                    AllowedRequests = 0, // Would need to track this
                    BlockedRequests = 0, // Would need to track this
                    BlockRate = 0.0,
                    LastUpdated = _dateTimeProvider.UtcNow
                };
                
                stats[resourceType] = statsData;
            }
            
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rate limiter stats");
            return new Dictionary<string, RateLimitStats>();
        }
    }

    #region Private Methods

    private RateLimitConfig GetConfig(string resourceType)
    {
        if (string.IsNullOrEmpty(resourceType))
        {
            return GetDefaultConfig();
        }

        if (_configs.TryGetValue(resourceType, out var config))
        {
            return config;
        }

        // Return default config if not found
        return GetDefaultConfig();
    }

    private RateLimitConfig GetDefaultConfig()
    {
        return new RateLimitConfig
        {
            ResourceType = "default",
            Limit = _config.DefaultMessageLimit,
            Window = _config.DefaultWindow,
            BurstLimit = Math.Min(_config.DefaultMessageLimit / 10, 10),
            StrictMode = _config.StrictMode
        };
    }

    private async Task<int> GetCurrentUsageAsync(string key, RateLimitConfig config)
    {
        if (string.IsNullOrEmpty(key))
        {
            return 0;
        }

        try
        {
            var usage = await _redisService.GetAsync(key);
            if (string.IsNullOrEmpty(usage))
                return 0;

            if (int.TryParse(usage, out var currentUsage))
            {
                // Check if the window has expired
                var expiryKey = $"{key}:expiry";
                var expiryStr = await _redisService.GetAsync(expiryKey);
                
                if (!string.IsNullOrEmpty(expiryStr) && DateTime.TryParse(expiryStr, out var expiry))
                {
                    if (_dateTimeProvider.UtcNow > expiry)
                    {
                        // Window expired, reset usage
                        await _redisService.DeleteAsync(key);
                        await _redisService.DeleteAsync(expiryKey);
                        return 0;
                    }
                }
                
                return currentUsage;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current usage for key {Key}", key);
            return 0;
        }
    }

    private async Task IncrementUsageAsync(string key, int cost, TimeSpan window)
    {
        if (string.IsNullOrEmpty(key) || cost <= 0)
        {
            return;
        }

        try
        {
            var currentUsage = await GetCurrentUsageAsync(key, new RateLimitConfig { Window = window });
            var newUsage = currentUsage + cost;
            
            await _redisService.SetAsync(key, newUsage.ToString(), window);
            
            // Set expiry time
            var expiryKey = $"{key}:expiry";
            var expiry = _dateTimeProvider.UtcNow.Add(window);
            await _redisService.SetAsync(expiryKey, expiry.ToString("O"), window);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing usage for key {Key}", key);
        }
    }

    private async Task<DateTime> GetResetTimeAsync(string key, TimeSpan window)
    {
        if (string.IsNullOrEmpty(key))
        {
            return _dateTimeProvider.UtcNow.Add(window);
        }

        try
        {
            var expiryKey = $"{key}:expiry";
            var expiryStr = await _redisService.GetAsync(expiryKey);
            
            if (!string.IsNullOrEmpty(expiryStr) && DateTime.TryParse(expiryStr, out var expiry))
            {
                return expiry;
            }
            
            // If no expiry found, calculate based on window
            return _dateTimeProvider.UtcNow.Add(window);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reset time for key {Key}", key);
            return _dateTimeProvider.UtcNow.Add(window);
        }
    }

    private async Task<(string key, bool result)> CheckLimitWithKeyAsync(string key, string resourceType, int cost)
    {
        var result = await CheckLimitAsync(key, resourceType, cost);
        return (key, result);
    }

    #endregion
}

public class RateLimiterConfig
{
    public int DefaultMessageLimit { get; set; } = 100;
    public int DefaultTypingLimit { get; set; } = 60;
    public int DefaultRoomOperationsLimit { get; set; } = 30;
    public int DefaultPresenceLimit { get; set; } = 120;
    public TimeSpan DefaultWindow { get; set; } = TimeSpan.FromMinutes(1);
    public bool EnableBurstLimiting { get; set; } = true;
    public bool StrictMode { get; set; } = false;
}
