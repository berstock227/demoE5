using ChatPlatform.Core.Interfaces;
using ChatPlatform.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ChatPlatform.Services.Implementations;

public class ConnectionManagerService : IConnectionManager
{
    private readonly IRedisService _redisService;
    private readonly ILogger<ConnectionManagerService> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ConnectionManagerConfig _config;
    
    // In-memory connection tracking for fast access
    private readonly ConcurrentDictionary<string, ConnectionInfo> _connections = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _userConnections = new(); // userId -> connectionIds
    private readonly ConcurrentDictionary<string, HashSet<string>> _roomConnections = new(); // roomId -> connectionIds
    private readonly ConcurrentDictionary<string, HashSet<string>> _tenantConnections = new(); // tenantId -> connectionIds

    public ConnectionManagerService(
        IRedisService redisService,
        ILogger<ConnectionManagerService> logger,
        IDateTimeProvider dateTimeProvider,
        IOptions<ConnectionManagerConfig> config)
    {
        _redisService = redisService ?? throw new ArgumentNullException(nameof(redisService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        
        // Start cleanup task
        _ = StartCleanupTaskAsync();
    }

    #region Connection Management

    public async Task<bool> AddConnectionAsync(string connectionId, string userId, string tenantId, string nodeId)
    {
        if (string.IsNullOrEmpty(connectionId) || string.IsNullOrEmpty(userId) || 
            string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(nodeId))
        {
            _logger.LogWarning("Invalid parameters for AddConnectionAsync");
            return false;
        }

        try
        {
            var connectionInfo = new ConnectionInfo
            {
                ConnectionId = connectionId,
                UserId = userId,
                TenantId = tenantId,
                NodeId = nodeId,
                ConnectedAt = _dateTimeProvider.UtcNow,
                LastActivityAt = _dateTimeProvider.UtcNow,
                Metadata = new Dictionary<string, object>()
            };

            // Add to in-memory collections
            _connections.TryAdd(connectionId, connectionInfo);
            
            _userConnections.AddOrUpdate(userId, 
                new HashSet<string> { connectionId },
                (_, connections) => { connections.Add(connectionId); return connections; });
            
            _tenantConnections.AddOrUpdate(tenantId,
                new HashSet<string> { connectionId },
                (_, connections) => { connections.Add(connectionId); return connections; });

            // Store in Redis for persistence across nodes
            var connectionKey = $"connection:{connectionId}";
            await _redisService.SetAsync(connectionKey, connectionInfo, _config.ConnectionPersistenceTimeout);

            // Update user's connection count
            var userConnectionsKey = $"user_connections:{tenantId}:{userId}";
            await _redisService.SetAddAsync(userConnectionsKey, connectionId);
            await _redisService.ExpireAsync(userConnectionsKey, _config.ConnectionPersistenceTimeout);

            // Update tenant's connection count
            var tenantConnectionsKey = $"tenant_connections:{tenantId}";
            await _redisService.SetAddAsync(tenantConnectionsKey, connectionId);
            await _redisService.ExpireAsync(tenantConnectionsKey, _config.ConnectionPersistenceTimeout);

            _logger.LogDebug("Connection {ConnectionId} added for user {UserId} in tenant {TenantId} on node {NodeId}", 
                connectionId, userId, tenantId, nodeId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding connection {ConnectionId} for user {UserId}", connectionId, userId);
            return false;
        }
    }

    public async Task<bool> RemoveConnectionAsync(string connectionId)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            _logger.LogWarning("Invalid connectionId for RemoveConnectionAsync");
            return false;
        }

        try
        {
            if (!_connections.TryRemove(connectionId, out var connectionInfo))
            {
                _logger.LogDebug("Connection {ConnectionId} not found for removal", connectionId);
                return false;
            }

            var userId = connectionInfo.UserId;
            var tenantId = connectionInfo.TenantId;

            // Remove from in-memory collections
            RemoveFromUserConnections(userId, connectionId);
            RemoveFromTenantConnections(tenantId, connectionId);
            await RemoveFromRoomConnections(connectionId);

            // Remove from Redis
            var connectionKey = $"connection:{connectionId}";
            await _redisService.DeleteAsync(connectionKey);

            // Update user's connection count
            var userConnectionsKey = $"user_connections:{tenantId}:{userId}";
            await _redisService.SetRemoveAsync(userConnectionsKey, connectionId);

            // Update tenant's connection count
            var tenantConnectionsKey = $"tenant_connections:{tenantId}";
            await _redisService.SetRemoveAsync(tenantConnectionsKey, connectionId);

            _logger.LogDebug("Connection {ConnectionId} removed for user {UserId} in tenant {TenantId}", 
                connectionId, userId, tenantId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing connection {ConnectionId}", connectionId);
            return false;
        }
    }

    public async Task<ConnectionInfo?> GetConnectionAsync(string connectionId)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            return null;
        }

        try
        {
            // Try in-memory first
            if (_connections.TryGetValue(connectionId, out var connectionInfo))
            {
                return connectionInfo;
            }

            // Fallback to Redis
            var connectionKey = $"connection:{connectionId}";
            var redisConnection = await _redisService.GetAsync<ConnectionInfo>(connectionKey);
            
            if (redisConnection != null)
            {
                // Add back to in-memory cache
                _connections.TryAdd(connectionId, redisConnection);
                return redisConnection;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connection {ConnectionId}", connectionId);
            return null;
        }
    }

    public async Task<List<ConnectionInfo>> GetUserConnectionsAsync(string userId, string tenantId)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId))
        {
            return new List<ConnectionInfo>();
        }

        try
        {
            var connections = new List<ConnectionInfo>();

            // Get from in-memory
            if (_userConnections.TryGetValue(userId, out var connectionIds))
            {
                foreach (var connectionId in connectionIds)
                {
                    if (_connections.TryGetValue(connectionId, out var connectionInfo))
                    {
                        connections.Add(connectionInfo);
                    }
                }
            }

            // If no in-memory connections, try Redis
            if (connections.Count == 0)
            {
                var userConnectionsKey = $"user_connections:{tenantId}:{userId}";
                var redisConnectionIds = await _redisService.SetMembersAsync(userConnectionsKey);
                
                foreach (var connectionId in redisConnectionIds)
                {
                    var connectionInfo = await GetConnectionAsync(connectionId);
                    if (connectionInfo != null)
                    {
                        connections.Add(connectionInfo);
                    }
                }
            }

            return connections;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connections for user {UserId} in tenant {TenantId}", userId, tenantId);
            return new List<ConnectionInfo>();
        }
    }

    public async Task<List<ConnectionInfo>> GetTenantConnectionsAsync(string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
        {
            return new List<ConnectionInfo>();
        }

        try
        {
            var connections = new List<ConnectionInfo>();

            // Get from in-memory
            if (_tenantConnections.TryGetValue(tenantId, out var connectionIds))
            {
                foreach (var connectionId in connectionIds)
                {
                    if (_connections.TryGetValue(connectionId, out var connectionInfo))
                    {
                        connections.Add(connectionInfo);
                    }
                }
            }

            // If no in-memory connections, try Redis
            if (connections.Count == 0)
            {
                var tenantConnectionsKey = $"tenant_connections:{tenantId}";
                var redisConnectionIds = await _redisService.SetMembersAsync(tenantConnectionsKey);
                
                foreach (var connectionId in redisConnectionIds)
                {
                    var connectionInfo = await GetConnectionAsync(connectionId);
                    if (connectionInfo != null)
                    {
                        connections.Add(connectionInfo);
                    }
                }
            }

            return connections;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connections for tenant {TenantId}", tenantId);
            return new List<ConnectionInfo>();
        }
    }

    public async Task<int> GetConnectionCountAsync(string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
        {
            return 0;
        }

        try
        {
            // Try in-memory first
            if (_tenantConnections.TryGetValue(tenantId, out var connectionIds))
            {
                return connectionIds.Count;
            }

            // Fallback to Redis
            var tenantConnectionsKey = $"tenant_connections:{tenantId}";
            return (int)await _redisService.SetLengthAsync(tenantConnectionsKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connection count for tenant {TenantId}", tenantId);
            return 0;
        }
    }

    #endregion

    #region User Sessions

    public async Task<bool> UpdateUserActivityAsync(string connectionId)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            return false;
        }

        try
        {
            if (_connections.TryGetValue(connectionId, out var connectionInfo))
            {
                connectionInfo.LastActivityAt = _dateTimeProvider.UtcNow;
                
                // Update in Redis
                var connectionKey = $"connection:{connectionId}";
                await _redisService.SetAsync(connectionKey, connectionInfo, _config.ConnectionPersistenceTimeout);
                
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating activity for connection {ConnectionId}", connectionId);
            return false;
        }
    }

    public async Task<bool> UpdateUserPresenceAsync(string userId, string tenantId, PresenceStatus status)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId))
        {
            return false;
        }

        try
        {
            var presenceKey = $"presence:{tenantId}:{userId}";
            var presence = new PresenceUpdate
            {
                UserId = userId,
                Status = status,
                LastSeen = _dateTimeProvider.UtcNow
            };

            await _redisService.SetAsync(presenceKey, presence, TimeSpan.FromMinutes(30));
            
            _logger.LogDebug("Presence updated for user {UserId} in tenant {TenantId}: {Status}", userId, tenantId, status);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating presence for user {UserId} in tenant {TenantId}", userId, tenantId);
            return false;
        }
    }

    public async Task<PresenceStatus> GetUserPresenceAsync(string userId, string tenantId)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId))
        {
            return PresenceStatus.Offline;
        }

        try
        {
            var presenceKey = $"presence:{tenantId}:{userId}";
            var presence = await _redisService.GetAsync<PresenceUpdate>(presenceKey);
            
            if (presence != null)
            {
                // Check if presence is still valid (not expired)
                if (_dateTimeProvider.UtcNow - presence.LastSeen < TimeSpan.FromMinutes(30))
                {
                    return presence.Status;
                }
            }

            // Check if user has active connections
            var connections = await GetUserConnectionsAsync(userId, tenantId);
            return connections.Any() ? PresenceStatus.Online : PresenceStatus.Offline;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting presence for user {UserId} in tenant {TenantId}", userId, tenantId);
            return PresenceStatus.Offline;
        }
    }

    #endregion

    #region Room Management

    public async Task<bool> JoinRoomAsync(string connectionId, string roomId, string tenantId)
    {
        if (string.IsNullOrEmpty(connectionId) || string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(tenantId))
        {
            return false;
        }

        try
        {
            // Add to in-memory room connections
            _roomConnections.AddOrUpdate(roomId,
                new HashSet<string> { connectionId },
                (_, connections) => { connections.Add(connectionId); return connections; });

            // Store in Redis
            var roomConnectionsKey = $"room_connections:{tenantId}:{roomId}";
            await _redisService.SetAddAsync(roomConnectionsKey, connectionId);
            await _redisService.ExpireAsync(roomConnectionsKey, _config.ConnectionPersistenceTimeout);

            _logger.LogDebug("Connection {ConnectionId} joined room {RoomId} in tenant {TenantId}", connectionId, roomId, tenantId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining room {RoomId} for connection {ConnectionId}", roomId, connectionId);
            return false;
        }
    }

    public async Task<bool> LeaveRoomAsync(string connectionId, string roomId, string tenantId)
    {
        if (string.IsNullOrEmpty(connectionId) || string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(tenantId))
        {
            return false;
        }

        try
        {
            // Remove from in-memory room connections
            if (_roomConnections.TryGetValue(roomId, out var roomConnections))
            {
                roomConnections.Remove(connectionId);
                if (roomConnections.Count == 0)
                {
                    _roomConnections.TryRemove(roomId, out _);
                }
            }

            // Remove from Redis
            var roomConnectionsKey = $"room_connections:{tenantId}:{roomId}";
            await _redisService.SetRemoveAsync(roomConnectionsKey, connectionId);

            _logger.LogDebug("Connection {ConnectionId} left room {RoomId} in tenant {TenantId}", connectionId, roomId, tenantId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving room {RoomId} for connection {ConnectionId}", roomId, connectionId);
            return false;
        }
    }

    public async Task<List<string>> GetUserRoomsAsync(string connectionId)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            return new List<string>();
        }

        try
        {
            var rooms = new List<string>();

            // Check in-memory room connections
            foreach (var kvp in _roomConnections)
            {
                if (kvp.Value.Contains(connectionId))
                {
                    rooms.Add(kvp.Key);
                }
            }

            // If no in-memory rooms, this might be a new connection - return empty list
            return rooms;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rooms for connection {ConnectionId}", connectionId);
            return new List<string>();
        }
    }

    public async Task<List<string>> GetRoomConnectionsAsync(string roomId, string tenantId)
    {
        if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(tenantId))
        {
            return new List<string>();
        }

        try
        {
            var connections = new List<string>();

            // Get from in-memory
            if (_roomConnections.TryGetValue(roomId, out var connectionIds))
            {
                connections.AddRange(connectionIds);
            }

            // If no in-memory connections, try Redis
            if (connections.Count == 0)
            {
                var roomConnectionsKey = $"room_connections:{tenantId}:{roomId}";
                var redisConnectionIds = await _redisService.SetMembersAsync(roomConnectionsKey);
                connections.AddRange(redisConnectionIds);
            }

            return connections;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connections for room {RoomId} in tenant {TenantId}", roomId, tenantId);
            return new List<string>();
        }
    }

    #endregion

    #region Broadcasting

    public async Task BroadcastToRoomAsync(string roomId, string tenantId, object message, string? excludeConnectionId = null)
    {
        if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(tenantId) || message == null)
        {
            return;
        }

        try
        {
            var connections = await GetRoomConnectionsAsync(roomId, tenantId);
            var channelKey = $"room:{tenantId}:{roomId}";
            
            // Publish to Redis channel for other nodes
            await _redisService.PublishAsync(channelKey, message);
            
            _logger.LogDebug("Broadcasted message to room {RoomId} in tenant {TenantId}, {Connections} connections", 
                roomId, tenantId, connections.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting to room {RoomId} in tenant {TenantId}", roomId, tenantId);
        }
    }

    public async Task BroadcastToUserAsync(string userId, string tenantId, object message)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId) || message == null)
        {
            return;
        }

        try
        {
            var connections = await GetUserConnectionsAsync(userId, tenantId);
            var channelKey = $"user:{tenantId}:{userId}";
            
            // Publish to Redis channel for other nodes
            await _redisService.PublishAsync(channelKey, message);
            
            _logger.LogDebug("Broadcasted message to user {UserId} in tenant {TenantId}, {Connections} connections", 
                userId, tenantId, connections.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting to user {UserId} in tenant {TenantId}", userId, tenantId);
        }
    }

    public async Task BroadcastToTenantAsync(string tenantId, object message)
    {
        if (string.IsNullOrEmpty(tenantId) || message == null)
        {
            return;
        }

        try
        {
            var connections = await GetTenantConnectionsAsync(tenantId);
            var channelKey = $"tenant:{tenantId}";
            
            // Publish to Redis channel for other nodes
            await _redisService.PublishAsync(channelKey, message);
            
            _logger.LogDebug("Broadcasted message to tenant {TenantId}, {Connections} connections", tenantId, connections.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting to tenant {TenantId}", tenantId);
        }
    }

    #endregion

    #region Health & Monitoring

    public async Task<ConnectionManagerHealth> GetHealthAsync()
    {
        try
        {
            var redisHealth = await _redisService.GetHealthAsync();
            
            var connectionsByTenant = new Dictionary<string, int>();
            foreach (var kvp in _tenantConnections)
            {
                connectionsByTenant[kvp.Key] = kvp.Value.Count;
            }

            var connectionsByNode = new Dictionary<string, int>();
            foreach (var connection in _connections.Values)
            {
                if (connectionsByNode.ContainsKey(connection.NodeId))
                {
                    connectionsByNode[connection.NodeId]++;
                }
                else
                {
                    connectionsByNode[connection.NodeId] = 1;
                }
            }

            return new ConnectionManagerHealth
            {
                IsHealthy = redisHealth.IsHealthy,
                TotalConnections = _connections.Count,
                ActiveConnections = _connections.Count(c => c.Value.LastActivityAt > _dateTimeProvider.UtcNow.AddMinutes(-5)),
                ConnectionsByTenant = connectionsByTenant,
                ConnectionsByNode = connectionsByNode,
                CheckedAt = _dateTimeProvider.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connection manager health");
            return new ConnectionManagerHealth
            {
                IsHealthy = false,
                TotalConnections = 0,
                ActiveConnections = 0,
                ConnectionsByTenant = new Dictionary<string, int>(),
                ConnectionsByNode = new Dictionary<string, int>(),
                CheckedAt = _dateTimeProvider.UtcNow
            };
        }
    }

    public async Task CleanupInactiveConnectionsAsync()
    {
        try
        {
            var inactiveThreshold = _dateTimeProvider.UtcNow.AddMinutes(-_config.InactiveConnectionTimeoutMinutes);
            var inactiveConnections = _connections.Values
                .Where(c => c.LastActivityAt < inactiveThreshold)
                .ToList();

            foreach (var connection in inactiveConnections)
            {
                await RemoveConnectionAsync(connection.ConnectionId);
            }

            if (inactiveConnections.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} inactive connections", inactiveConnections.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up inactive connections");
        }
    }

    public async Task<Dictionary<string, int>> GetConnectionStatsAsync()
    {
        try
        {
            var stats = new Dictionary<string, int>
            {
                ["total_connections"] = _connections.Count,
                ["total_users"] = _userConnections.Count,
                ["total_rooms"] = _roomConnections.Count,
                ["total_tenants"] = _tenantConnections.Count
            };

            // Add per-tenant stats
            foreach (var kvp in _tenantConnections)
            {
                stats[$"tenant_{kvp.Key}_connections"] = kvp.Value.Count;
            }

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connection stats");
            return new Dictionary<string, int>();
        }
    }

    #endregion

    #region Private Methods

    private void RemoveFromUserConnections(string userId, string connectionId)
    {
        if (_userConnections.TryGetValue(userId, out var userConnections))
        {
            userConnections.Remove(connectionId);
            if (userConnections.Count == 0)
            {
                _userConnections.TryRemove(userId, out _);
            }
        }
    }

    private void RemoveFromTenantConnections(string tenantId, string connectionId)
    {
        if (_tenantConnections.TryGetValue(tenantId, out var tenantConnections))
        {
            tenantConnections.Remove(connectionId);
            if (tenantConnections.Count == 0)
            {
                _tenantConnections.TryRemove(tenantId, out _);
            }
        }
    }

    private async Task RemoveFromRoomConnections(string connectionId)
    {
        var roomsToRemove = new List<string>();
        
        foreach (var kvp in _roomConnections)
        {
            if (kvp.Value.Contains(connectionId))
            {
                roomsToRemove.Add(kvp.Key);
            }
        }

        foreach (var roomId in roomsToRemove)
        {
            if (_roomConnections.TryGetValue(roomId, out var roomConnections))
            {
                roomConnections.Remove(connectionId);
                if (roomConnections.Count == 0)
                {
                    _roomConnections.TryRemove(roomId, out _);
                }
            }
        }
    }

    private async Task StartCleanupTaskAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_config.CleanupIntervalMinutes));
                await CleanupInactiveConnectionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cleanup task");
            }
        }
    }

    #endregion
}

public class ConnectionManagerConfig
{
    public int InactiveConnectionTimeoutMinutes { get; set; } = 30;
    public int CleanupIntervalMinutes { get; set; } = 5;
    public int MaxConnectionsPerUser { get; set; } = 5;
    public int MaxConnectionsPerTenant { get; set; } = 10000;
    public bool EnableConnectionPersistence { get; set; } = true;
    public TimeSpan ConnectionPersistenceTimeout { get; set; } = TimeSpan.FromHours(24);
}
