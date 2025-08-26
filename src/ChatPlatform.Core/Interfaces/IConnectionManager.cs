using ChatPlatform.Core.Models;

namespace ChatPlatform.Core.Interfaces;

public interface IConnectionManager
{
    // Connection Management
    Task<bool> AddConnectionAsync(string connectionId, string userId, string tenantId, string nodeId);
    Task<bool> RemoveConnectionAsync(string connectionId);
    Task<ConnectionInfo?> GetConnectionAsync(string connectionId);
    Task<List<ConnectionInfo>> GetUserConnectionsAsync(string userId, string tenantId);
    Task<List<ConnectionInfo>> GetTenantConnectionsAsync(string tenantId);
    Task<int> GetConnectionCountAsync(string tenantId);
    
    // User Sessions
    Task<bool> UpdateUserActivityAsync(string connectionId);
    Task<bool> UpdateUserPresenceAsync(string userId, string tenantId, PresenceStatus status);
    Task<PresenceStatus> GetUserPresenceAsync(string userId, string tenantId);
    
    // Room Management
    Task<bool> JoinRoomAsync(string connectionId, string roomId, string tenantId);
    Task<bool> LeaveRoomAsync(string connectionId, string roomId, string tenantId);
    Task<List<string>> GetUserRoomsAsync(string connectionId);
    Task<List<string>> GetRoomConnectionsAsync(string roomId, string tenantId);
    
    // Broadcasting
    Task BroadcastToRoomAsync(string roomId, string tenantId, object message, string? excludeConnectionId = null);
    Task BroadcastToUserAsync(string userId, string tenantId, object message);
    Task BroadcastToTenantAsync(string tenantId, object message);
    
    // Health & Monitoring
    Task<ConnectionManagerHealth> GetHealthAsync();
    Task CleanupInactiveConnectionsAsync();
    Task<Dictionary<string, int>> GetConnectionStatsAsync();
}

public class ConnectionManagerHealth
{
    public bool IsHealthy { get; set; }
    public int TotalConnections { get; set; }
    public int ActiveConnections { get; set; }
    public Dictionary<string, int> ConnectionsByTenant { get; set; } = new();
    public Dictionary<string, int> ConnectionsByNode { get; set; } = new();
    public DateTime CheckedAt { get; set; }
}

public class ConnectionEvent
{
    public string ConnectionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public ConnectionEventType EventType { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public enum ConnectionEventType
{
    Connected,
    Disconnected,
    JoinedRoom,
    LeftRoom,
    ActivityUpdate,
    PresenceUpdate
}
