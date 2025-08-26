namespace ChatPlatform.Core.Interfaces;

public interface IRateLimiter
{
    // Rate Limit Checking
    Task<bool> CheckLimitAsync(string key, string resourceType, int cost = 1);
    Task<RateLimitInfo> GetRateLimitInfoAsync(string key, string resourceType);
    
    // Rate Limit Management
    Task<bool> ResetLimitAsync(string key, string resourceType);
    Task<bool> SetLimitAsync(string key, string resourceType, int limit, TimeSpan window);
    
    // Bulk Operations
    Task<Dictionary<string, bool>> CheckMultipleLimitsAsync(Dictionary<string, (string resourceType, int cost)> requests);
    
    // Health & Monitoring
    Task<RateLimiterHealth> GetHealthAsync();
    Task<Dictionary<string, RateLimitStats>> GetStatsAsync();
}

public class RateLimitInfo
{
    public string Key { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public int Limit { get; set; }
    public int Remaining { get; set; }
    public int Used { get; set; }
    public DateTime ResetTime { get; set; }
    public bool IsAllowed { get; set; }
}

public class RateLimitStats
{
    public string ResourceType { get; set; } = string.Empty;
    public int TotalRequests { get; set; }
    public int AllowedRequests { get; set; }
    public int BlockedRequests { get; set; }
    public double BlockRate { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class RateLimiterHealth
{
    public bool IsHealthy { get; set; }
    public int TotalKeys { get; set; }
    public int ActiveKeys { get; set; }
    public Dictionary<string, int> KeysByResourceType { get; set; } = new();
    public DateTime CheckedAt { get; set; }
}

public class RateLimitConfig
{
    public string ResourceType { get; set; } = string.Empty;
    public int Limit { get; set; }
    public TimeSpan Window { get; set; }
    public int BurstLimit { get; set; }
    public bool StrictMode { get; set; }
}

public class RateLimitViolation
{
    public string Key { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public int AttemptedCost { get; set; }
    public int CurrentUsage { get; set; }
    public int Limit { get; set; }
    public DateTime ViolationTime { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
}
