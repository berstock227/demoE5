using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ChatPlatform.Core.Interfaces;
using ChatPlatform.Core.Models;
using System.Text.Json;

namespace ChatPlatform.SignalR.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly IConnectionManager _connectionManager;
    private readonly IRateLimiter _rateLimiter;
    private readonly ILogger<ChatHub> _logger;
    private readonly string _nodeId;

    public ChatHub(
        IChatService chatService,
        IConnectionManager connectionManager,
        IRateLimiter rateLimiter,
        ILogger<ChatHub> logger,
        IConfiguration configuration)
    {
        _chatService = chatService;
        _connectionManager = connectionManager;
        _rateLimiter = rateLimiter;
        _logger = logger;
        _nodeId = configuration["NodeId"] ?? "node-1";
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            _logger.LogInformation("OnConnectedAsync called for connection {ConnectionId}", Context.ConnectionId);
            
            // Log JWT claims for debugging
            if (Context.User?.Claims != null)
            {
                foreach (var claim in Context.User.Claims)
                {
                    _logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
                }
            }
            else
            {
                _logger.LogWarning("No claims found in Context.User");
            }
            
            var userId = GetUserIdFromContext();
            var tenantId = GetTenantIdFromContext();
            
            _logger.LogInformation("Extracted userId: {UserId}, tenantId: {TenantId}", userId, tenantId);
            
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId))
            {
                _logger.LogWarning("Missing userId or tenantId, aborting connection");
                Context.Abort();
                return;
            }

            // Add connection to manager
            await _connectionManager.AddConnectionAsync(Context.ConnectionId, userId, tenantId, _nodeId);
            
            // Update user presence
            await _connectionManager.UpdateUserPresenceAsync(userId, tenantId, PresenceStatus.Online);
            
            // Join user's default rooms
            await JoinUserDefaultRoomsAsync(userId, tenantId);
            
            // Notify other users in tenant about presence
            await Clients.OthersInGroup(tenantId).SendAsync("UserPresenceChanged", userId, PresenceStatus.Online);
            
            _logger.LogInformation("User {UserId} connected from {NodeId}", userId, _nodeId);
            
            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnConnectedAsync for connection {ConnectionId}", Context.ConnectionId);
            Context.Abort();
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var userId = GetUserIdFromContext();
            var tenantId = GetTenantIdFromContext();
            
            if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(tenantId))
            {
                _logger.LogInformation("User {UserId} disconnected from {NodeId}", userId, _nodeId);
                
                // Update user presence
                await _connectionManager.UpdateUserPresenceAsync(userId, tenantId, PresenceStatus.Offline);
                
                // Remove connection from manager
                await _connectionManager.RemoveConnectionAsync(Context.ConnectionId);
                
                // Notify other users about presence change
                await Clients.OthersInGroup(tenantId).SendAsync("UserPresenceChanged", userId, PresenceStatus.Offline);
                
                // Auto-leave from all rooms when disconnected
                var userRooms = await _chatService.GetUserRoomsAsync(userId, tenantId);
                foreach (var room in userRooms)
                {
                    try
                    {
                        // Leave room in connection manager
                        await _connectionManager.LeaveRoomAsync(Context.ConnectionId, room.Id, tenantId);
                        
                        // Remove from SignalR group
                        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{tenantId}:{room.Id}");
                        
                        // Update typing indicators
                        await _chatService.UpdateTypingAsync(room.Id, userId, false, tenantId);
                        
                        // Notify other users in room about auto-leave
                        await Clients.OthersInGroup($"{tenantId}:{room.Id}").SendAsync("UserLeftRoom", userId, room.Id);
                        
                        _logger.LogInformation("Auto-left room on disconnect. UserId: {UserId}, RoomId: {RoomId}", userId, room.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error auto-leaving room on disconnect. UserId: {UserId}, RoomId: {RoomId}", userId, room.Id);
                    }
                }
            }
            
            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnDisconnectedAsync for connection {ConnectionId}", Context.ConnectionId);
        }
    }

    [HubMethodName("joinRoom")]
    public async Task JoinRoom(string roomId)
    {
        try
        {
            var userId = GetUserIdFromContext();
            var tenantId = GetTenantIdFromContext();
            
            // Rate limiting
            var rateLimitKey = $"join_room:{userId}:{tenantId}";
            if (!await _rateLimiter.CheckLimitAsync(rateLimitKey, "room_operations"))
            {
                await Clients.Caller.SendAsync("Error", "Rate limit exceeded for room operations");
                return;
            }
            
            // Join room in connection manager
            var success = await _connectionManager.JoinRoomAsync(Context.ConnectionId, roomId, tenantId);
            if (!success)
            {
                await Clients.Caller.SendAsync("Error", "Failed to join room");
                return;
            }
            
            // Add to SignalR group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"{tenantId}:{roomId}");
            
            // Update typing indicators
            await _chatService.UpdateTypingAsync(roomId, userId, false, tenantId);
            
            // Notify other users in room
            await Clients.OthersInGroup($"{tenantId}:{roomId}").SendAsync("UserJoinedRoom", userId, roomId);
            
            await Clients.Caller.SendAsync("RoomJoined", roomId);
            
            _logger.LogInformation("User {UserId} joined room {RoomId}", userId, roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining room {RoomId} for user {UserId}", roomId, GetUserIdFromContext());
            await Clients.Caller.SendAsync("Error", "Failed to join room");
        }
    }

    [HubMethodName("leaveRoom")]
    public async Task LeaveRoom(string roomId)
    {
        try
        {
            var userId = GetUserIdFromContext();
            var tenantId = GetTenantIdFromContext();
            
            // Leave room in connection manager
            await _connectionManager.LeaveRoomAsync(Context.ConnectionId, roomId, tenantId);
            
            // Remove from SignalR group
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{tenantId}:{roomId}");
            
            // Update typing indicators
            await _chatService.UpdateTypingAsync(roomId, userId, false, tenantId);
            
            // Notify other users in room
            await Clients.OthersInGroup($"{tenantId}:{roomId}").SendAsync("UserLeftRoom", userId, roomId);
            
            await Clients.Caller.SendAsync("RoomLeft", roomId);
            
            _logger.LogInformation("User {UserId} left room {RoomId}", userId, roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving room {RoomId} for user {UserId}", roomId, GetUserIdFromContext());
        }
    }

    [HubMethodName("sendMessage")]
    public async Task SendMessage(string roomId, string content)
    {
        try
        {
            var userId = GetUserIdFromContext();
            var tenantId = GetTenantIdFromContext();
            
            // Rate limiting
            var rateLimitKey = $"send_message:{userId}:{tenantId}";
            if (!await _rateLimiter.CheckLimitAsync(rateLimitKey, "message"))
            {
                await Clients.Caller.SendAsync("Error", "Rate limit exceeded for messages");
                return;
            }
            
            // Create message request
            var request = new SendMessageRequest
            {
                Content = content,
                MessageType = MessageType.Text, // Default to text type
                RoomId = roomId,
                SenderId = userId,
                TenantId = tenantId
            };
            
            // Send message through chat service
            var response = await _chatService.SendMessageAsync(request);
            if (!response.Success)
            {
                await Clients.Caller.SendAsync("Error", response.ErrorMessage ?? "Failed to send message");
                return;
            }
            
            // Broadcast message to room
            var messageData = new
            {
                messageId = response.MessageId,
                content = content,
                type = "text", // Default to text type
                senderId = userId,
                roomId = roomId,
                timestamp = DateTime.UtcNow,
                metadata = new Dictionary<string, object>()
            };
            
            await Clients.Group($"{tenantId}:{roomId}").SendAsync("MessageReceived", messageData);
            
            _logger.LogInformation("Message sent by {UserId} in room {RoomId}", userId, roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message in room {RoomId} by user {UserId}", roomId, GetUserIdFromContext());
            await Clients.Caller.SendAsync("Error", "Failed to send message");
        }
    }

    [HubMethodName("typing")]
    public async Task UpdateTyping(string roomId, bool isTyping)
    {
        try
        {
            var userId = GetUserIdFromContext();
            var tenantId = GetTenantIdFromContext();
            
            // Rate limiting for typing indicators
            var rateLimitKey = $"typing:{userId}:{tenantId}";
            if (!await _rateLimiter.CheckLimitAsync(rateLimitKey, "typing"))
            {
                return; // Silently ignore typing rate limit violations
            }
            
            // Update typing indicator
            await _chatService.UpdateTypingAsync(roomId, userId, isTyping, tenantId);
            
            // Notify other users in room
            await Clients.OthersInGroup($"{tenantId}:{roomId}").SendAsync("UserTyping", userId, roomId, isTyping);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating typing indicator for user {UserId} in room {RoomId}", GetUserIdFromContext(), roomId);
        }
    }

    [HubMethodName("markAsRead")]
    public async Task MarkMessageAsRead(string messageId, string roomId)
    {
        try
        {
            var userId = GetUserIdFromContext();
            var tenantId = GetTenantIdFromContext();
            
            // Mark message as read
            var success = await _chatService.MarkMessageAsReadAsync(messageId, userId, roomId, tenantId);
            if (success)
            {
                // Notify other users in room about read receipt
                await Clients.OthersInGroup($"{tenantId}:{roomId}").SendAsync("MessageRead", messageId, userId, roomId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking message {MessageId} as read by user {UserId}", messageId, GetUserIdFromContext());
        }
    }

    [HubMethodName("updatePresence")]
    public async Task UpdatePresence(string status, string? customStatus = null)
    {
        try
        {
            var userId = GetUserIdFromContext();
            var tenantId = GetTenantIdFromContext();
            
            var presenceStatus = Enum.Parse<PresenceStatus>(status, true);
            
            // Update presence
            await _connectionManager.UpdateUserPresenceAsync(userId, tenantId, presenceStatus);
            await _chatService.UpdatePresenceAsync(userId, presenceStatus, customStatus, tenantId);
            
            // Notify other users in tenant
            await Clients.OthersInGroup(tenantId).SendAsync("UserPresenceChanged", userId, presenceStatus, customStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating presence for user {UserId}", GetUserIdFromContext());
        }
    }

    [HubMethodName("ping")]
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("pong", DateTime.UtcNow);
    }

    private string? GetUserIdFromContext()
    {
        // Try different claim types for user ID
        var userId = Context.User?.FindFirst("nameid")?.Value ?? 
                    Context.User?.FindFirst("sub")?.Value ?? 
                    Context.User?.FindFirst("user_id")?.Value ??
                    Context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        
        return userId;
    }

    private string? GetTenantIdFromContext()
    {
        return Context.User?.FindFirst("tenant_id")?.Value ?? 
               Context.User?.FindFirst("tid")?.Value ?? 
               "demo-tenant";
    }

    private async Task JoinUserDefaultRoomsAsync(string userId, string tenantId)
    {
        try
        {
            // Get user's rooms and join them
            var rooms = await _chatService.GetUserRoomsAsync(userId, tenantId);
            foreach (var room in rooms.Take(10)) // Limit to first 10 rooms
            {
                await _connectionManager.JoinRoomAsync(Context.ConnectionId, room.Id, tenantId);
                await Groups.AddToGroupAsync(Context.ConnectionId, $"{tenantId}:{room.Id}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining default rooms for user {UserId}", userId);
        }
    }
}
