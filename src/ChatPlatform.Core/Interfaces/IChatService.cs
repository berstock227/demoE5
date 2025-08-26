using ChatPlatform.Core.Models;

namespace ChatPlatform.Core.Interfaces;

public interface IChatService
{
    // Message Operations
    Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request);
    Task<Message> GetMessageAsync(string messageId, string tenantId);
    Task<List<Message>> GetRoomMessagesAsync(string roomId, string tenantId, int limit = 50, int offset = 0);
    Task<bool> DeleteMessageAsync(string messageId, string userId, string tenantId);
    Task<bool> UpdateMessageAsync(string messageId, string content, string userId, string tenantId);
    
    // Room Operations
    Task<Room> CreateRoomAsync(CreateRoomRequest request);
    Task<Room> GetRoomAsync(string roomId, string tenantId);
    Task<List<Room>> GetUserRoomsAsync(string userId, string tenantId);
    Task<List<Room>> GetAllRoomsAsync(string tenantId);
    Task<bool> UpdateRoomAsync(string roomId, UpdateRoomRequest request, string userId, string tenantId);
    Task<bool> DeleteRoomAsync(string roomId, string userId, string tenantId);
    
    // Room Membership
    Task<bool> JoinRoomAsync(string roomId, string userId, string tenantId);
    Task<bool> LeaveRoomAsync(string roomId, string userId, string tenantId);
    Task<List<RoomMember>> GetRoomMembersAsync(string roomId, string tenantId);
    Task<bool> AddMemberToRoomAsync(string roomId, string userId, string addedBy, string tenantId);
    Task<bool> RemoveMemberFromRoomAsync(string roomId, string userId, string removedBy, string tenantId);
    Task<bool> UpdateMemberRoleAsync(string roomId, string userId, UserRole newRole, string updatedBy, string tenantId);
    
    // Typing Indicators
    Task<bool> UpdateTypingAsync(string roomId, string userId, bool isTyping, string tenantId);
    Task<List<TypingIndicator>> GetTypingIndicatorsAsync(string roomId, string tenantId);
    
    // Presence
    Task<bool> UpdatePresenceAsync(string userId, PresenceStatus status, string? customStatus, string tenantId);
    Task<PresenceUpdate> GetUserPresenceAsync(string userId, string tenantId);
    Task<List<PresenceUpdate>> GetRoomPresenceAsync(string roomId, string tenantId);
    
    // Read Receipts
    Task<bool> MarkMessageAsReadAsync(string messageId, string userId, string roomId, string tenantId);
    Task<List<ReadReceipt>> GetMessageReadReceiptsAsync(string messageId, string tenantId);
    
    // Health Check
    Task<HealthCheckResponse> HealthCheckAsync();
}
