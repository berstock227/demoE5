using ChatPlatform.Core.Common;
using ChatPlatform.Core.Interfaces;
using ChatPlatform.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

using CoreModels = ChatPlatform.Core.Models;
using CoreInterfaces = ChatPlatform.Core.Interfaces;

namespace ChatPlatform.Services.Implementations;

public class ChatService : CoreInterfaces.IChatService
{
    private readonly IRedisService _redisService;
    private readonly ILogger<ChatService> _logger;
    private readonly IOptions<ChatServiceOptions> _options;

    public ChatService(
        IRedisService redisService,
        ILogger<ChatService> logger,
        IOptions<ChatServiceOptions> options)
    {
        _redisService = redisService;
        _logger = logger;
        _options = options;
    }

    public async Task<SendMessageResponse> SendMessageAsync(CoreModels.SendMessageRequest request)
    {
        try
        {
            // Generate message ID if not provided
            var messageId = request.MessageId ?? Guid.NewGuid().ToString();
            
            // Create message object
            var message = new Message
            {
                Id = messageId,
                Content = request.Content,
                Type = request.MessageType,
                RoomId = request.RoomId,
                SenderId = request.SenderId,
                TenantId = request.TenantId,
                CreatedAt = DateTime.UtcNow,
                Attachments = request.Attachments?.Select(a => new Attachment
                {
                    FileName = a.FileName,
                    FileUrl = a.FileUrl,
                    FileSize = a.FileSize,
                    ContentType = a.MimeType
                }).ToList()
            };

            // Store message in Redis
            var messageKey = $"message:{request.TenantId}:{messageId}";
            await _redisService.SetAsync(messageKey, message, TimeSpan.FromDays(30));

            // Add to room message list
            var roomMessagesKey = $"room_messages:{request.TenantId}:{request.RoomId}";
            await _redisService.ListLeftPushAsync(roomMessagesKey, messageId);

            // Publish to room channel
            var channel = $"room:{request.TenantId}:{request.RoomId}";
            await _redisService.PublishAsync(channel, $"message:{messageId}:{request.SenderId}:{request.RoomId}");

            _logger.LogInformation("Message sent successfully. MessageId: {MessageId}, RoomId: {RoomId}", messageId, request.RoomId);

            return new SendMessageResponse
            {
                Success = true,
                MessageId = messageId,
                Message = message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message. RoomId: {RoomId}, SenderId: {SenderId}", request.RoomId, request.SenderId);
            return new SendMessageResponse
            {
                Success = false,
                ErrorMessage = "Failed to send message"
            };
        }
    }

    public async Task<Message> GetMessageAsync(string messageId, string tenantId)
    {
        try
        {
            var messageKey = $"message:{tenantId}:{messageId}";
            var message = await _redisService.GetAsync<Message>(messageKey);
            
            if (message == null)
            {
                _logger.LogWarning("Message not found. MessageId: {MessageId}, TenantId: {TenantId}", messageId, tenantId);
            }

            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting message. MessageId: {MessageId}, TenantId: {TenantId}", messageId, tenantId);
            throw;
        }
    }

    public async Task<List<Message>> GetRoomMessagesAsync(string roomId, string tenantId, int limit = 50, int offset = 0)
    {
        try
        {
            var roomMessagesKey = $"room_messages:{tenantId}:{roomId}";
            var messageIds = await _redisService.ListRangeAsync<string>(roomMessagesKey, offset, offset + limit - 1);
            
            var messages = new List<Message>();
            foreach (var messageId in messageIds)
            {
                var message = await GetMessageAsync(messageId, tenantId);
                if (message != null)
                {
                    messages.Add(message);
                }
            }

            return messages.OrderByDescending(m => m.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting room messages. RoomId: {RoomId}, TenantId: {TenantId}", roomId, tenantId);
            throw;
        }
    }

    public async Task<bool> DeleteMessageAsync(string messageId, string userId, string tenantId)
    {
        try
        {
            // Get message to check ownership
            var message = await GetMessageAsync(messageId, tenantId);
            if (message == null || message.SenderId != userId)
            {
                return false;
            }

            // Delete message
            var messageKey = $"message:{tenantId}:{messageId}";
            await _redisService.DeleteAsync(messageKey);

            // Remove from room message list
            var roomMessagesKey = $"room_messages:{tenantId}:{message.RoomId}";
            await _redisService.ListRemoveAsync(roomMessagesKey, messageId);

            _logger.LogInformation("Message deleted successfully. MessageId: {MessageId}, UserId: {UserId}", messageId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message. MessageId: {MessageId}, UserId: {UserId}", messageId, userId);
            return false;
        }
    }

    public async Task<bool> UpdateMessageAsync(string messageId, string content, string userId, string tenantId)
    {
        try
        {
            // Get message to check ownership
            var message = await GetMessageAsync(messageId, tenantId);
            if (message == null || message.SenderId != userId)
            {
                return false;
            }

            // Update message content
            message.Content = content;
            message.EditedAt = DateTime.UtcNow;

            // Store updated message
            var messageKey = $"message:{tenantId}:{messageId}";
            await _redisService.SetAsync(messageKey, message, TimeSpan.FromDays(30));

            _logger.LogInformation("Message updated successfully. MessageId: {MessageId}, UserId: {UserId}", messageId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating message. MessageId: {MessageId}, UserId: {UserId}", messageId, userId);
            return false;
        }
    }

    public async Task<Room> CreateRoomAsync(CoreModels.CreateRoomRequest request)
    {
        try
        {

            var roomId = request.RoomId ?? Guid.NewGuid().ToString();
            
            var room = new Room
            {
                Id = roomId,
                Name = request.Name,
                Description = request.Description,
                Type = request.Type,
                CreatedBy = request.CreatedBy,
                TenantId = request.TenantId,
                CreatedAt = DateTime.UtcNow,
                Members = new List<RoomMember>
                {
                    new RoomMember
                    {
                        UserId = request.CreatedBy,
                        RoomId = roomId,
                        Role = UserRole.Admin,
                        JoinedAt = DateTime.UtcNow
                    }
                }
            };

            // Store room
            var roomKey = $"room:{request.TenantId}:{roomId}";
            await _redisService.SetAsync(roomKey, room, TimeSpan.FromDays(365));

            // Add to user's room list
            var userRoomsKey = $"user_rooms:{request.TenantId}:{request.CreatedBy}";
            await _redisService.SetAddAsync(userRoomsKey, roomId);

            // Add to tenant's room list (for GetAllRoomsAsync)
            var tenantRoomsKey = $"tenant_rooms:{request.TenantId}";
            await _redisService.SetAddAsync(tenantRoomsKey, roomId);

            // Publish room creation notification to tenant channel
            var tenantChannel = $"tenant:{request.TenantId}";
            await _redisService.PublishAsync(tenantChannel, $"room_created:{roomId}:{request.Name}");

            _logger.LogInformation("Room created successfully. RoomId: {RoomId}, Name: {Name}", roomId, request.Name);
            return room;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating room. Name: {Name}, CreatedBy: {CreatedBy}", request.Name, request.CreatedBy);
            throw;
        }
    }

    public async Task<Room> GetRoomAsync(string roomId, string tenantId)
    {
        try
        {
            var roomKey = $"room:{tenantId}:{roomId}";
            var room = await _redisService.GetAsync<Room>(roomKey);
            
            if (room == null)
            {
                _logger.LogWarning("Room not found. RoomId: {RoomId}, TenantId: {TenantId}", roomId, tenantId);
            }

            return room;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting room. RoomId: {RoomId}, TenantId: {TenantId}", roomId, tenantId);
            throw;
        }
    }

            public async Task<List<Room>> GetUserRoomsAsync(string userId, string tenantId)
        {
            try
            {
                var userRoomsKey = $"user_rooms:{tenantId}:{userId}";
                var roomIds = await _redisService.SetMembersAsync(userRoomsKey); // Use non-generic method for string IDs
                
                var rooms = new List<Room>();
                foreach (var roomId in roomIds)
                {
                    var room = await GetRoomAsync(roomId, tenantId);
                    if (room != null)
                    {
                        rooms.Add(room);
                    }
                }

                return rooms.OrderByDescending(r => r.CreatedAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user rooms. UserId: {UserId}, TenantId: {TenantId}", userId, tenantId);
                throw;
            }
        }

    public async Task<List<Room>> GetAllRoomsAsync(string tenantId)
    {
        try
        {
            var tenantRoomsKey = $"tenant_rooms:{tenantId}";
            var roomIds = await _redisService.SetMembersAsync(tenantRoomsKey);
            
            var rooms = new List<Room>();
            foreach (var roomId in roomIds)
            {
                var room = await GetRoomAsync(roomId, tenantId);
                if (room != null)
                {
                    rooms.Add(room);
                }
            }

            return rooms.OrderByDescending(r => r.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all rooms for tenant. TenantId: {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<bool> UpdateRoomAsync(string roomId, CoreModels.UpdateRoomRequest request, string userId, string tenantId)
    {
        try
        {
            // Get room to check ownership
            var room = await GetRoomAsync(roomId, tenantId);
            if (room == null || room.CreatedBy != userId)
            {
                return false;
            }

            // Update room properties
            if (!string.IsNullOrEmpty(request.Name))
                room.Name = request.Name;
            if (!string.IsNullOrEmpty(request.Description))
                room.Description = request.Description;
            if (request.Type != room.Type)
                room.Type = request.Type;

            room.UpdatedAt = DateTime.UtcNow;

            // Store updated room
            var roomKey = $"room:{tenantId}:{roomId}";
            await _redisService.SetAsync(roomKey, room, TimeSpan.FromDays(365));

            _logger.LogInformation("Room updated successfully. RoomId: {RoomId}, UserId: {UserId}", roomId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating room. RoomId: {RoomId}, UserId: {UserId}", roomId, userId);
            return false;
        }
    }

    public async Task<bool> DeleteRoomAsync(string roomId, string userId, string tenantId)
    {
        try
        {
            // Get room to check ownership
            var room = await GetRoomAsync(roomId, tenantId);
            if (room == null || room.CreatedBy != userId)
            {
                return false;
            }

            // Delete room
            var roomKey = $"room:{tenantId}:{roomId}";
            await _redisService.DeleteAsync(roomKey);

            // Remove from all users' room lists
            if (room.Members != null)
            {
                foreach (var member in room.Members)
                {
                    var userRoomsKey = $"user_rooms:{tenantId}:{member.UserId}";
                    await _redisService.SetRemoveAsync(userRoomsKey, roomId);
                }
            }

            _logger.LogInformation("Room deleted successfully. RoomId: {RoomId}, UserId: {UserId}", roomId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting room. RoomId: {RoomId}, UserId: {UserId}", roomId, userId);
            return false;
        }
    }

    public async Task<bool> JoinRoomAsync(string roomId, string userId, string tenantId)
    {
        try
        {
            var room = await GetRoomAsync(roomId, tenantId);
            if (room == null)
            {
                return false;
            }

            // Check if user is already a member
            if (room.Members?.Any(m => m.UserId == userId) == true)
            {
                return true; // Already a member
            }

            // Add user to room
            var member = new RoomMember
            {
                UserId = userId,
                RoomId = roomId,
                Role = UserRole.Member,
                JoinedAt = DateTime.UtcNow
            };

            room.Members ??= new List<RoomMember>();
            room.Members.Add(member);

            // Update room
            var roomKey = $"room:{tenantId}:{roomId}";
            await _redisService.SetAsync(roomKey, room, TimeSpan.FromDays(365));

            // Add room to user's room list
            var userRoomsKey = $"user_rooms:{tenantId}:{userId}";
            await _redisService.SetAddAsync(userRoomsKey, roomId);

            // Publish join notification to room channel
            var channel = $"room:{tenantId}:{roomId}";
            await _redisService.PublishAsync(channel, $"join:{userId}:{roomId}");

            _logger.LogInformation("User joined room successfully. UserId: {UserId}, RoomId: {RoomId}", userId, roomId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining room. UserId: {UserId}, RoomId: {RoomId}", userId, roomId);
            return false;
        }
    }

    public async Task<bool> LeaveRoomAsync(string roomId, string userId, string tenantId)
    {
        try
        {
            var room = await GetRoomAsync(roomId, tenantId);
            if (room == null)
            {
                return false;
            }

            // Remove user from room
            if (room.Members != null)
            {
                var member = room.Members.FirstOrDefault(m => m.UserId == userId);
                if (member != null)
                {
                    room.Members.Remove(member);
                }
            }

            // Update room
            var roomKey = $"room:{tenantId}:{roomId}";
            await _redisService.SetAsync(roomKey, room, TimeSpan.FromDays(365));

            // Remove room from user's room list
            var userRoomsKey = $"user_rooms:{tenantId}:{userId}";
            await _redisService.SetRemoveAsync(userRoomsKey, roomId);

            // Publish leave notification to room channel
            var channel = $"room:{tenantId}:{roomId}";
            await _redisService.PublishAsync(channel, $"leave:{userId}:{roomId}");

            _logger.LogInformation("User left room successfully. UserId: {UserId}, RoomId: {RoomId}", userId, roomId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving room. UserId: {UserId}, RoomId: {RoomId}", userId, roomId);
            return false;
        }
    }

    public async Task<List<RoomMember>> GetRoomMembersAsync(string roomId, string tenantId)
    {
        try
        {
            var room = await GetRoomAsync(roomId, tenantId);
            return room?.Members ?? new List<RoomMember>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting room members. RoomId: {RoomId}, TenantId: {TenantId}", roomId, tenantId);
            throw;
        }
    }

    public async Task<bool> AddMemberToRoomAsync(string roomId, string userId, string addedBy, string tenantId)
    {
        try
        {
            var room = await GetRoomAsync(roomId, tenantId);
            if (room == null || room.CreatedBy != addedBy)
            {
                return false;
            }

            // Check if user is already a member
            if (room.Members?.Any(m => m.UserId == userId) == true)
            {
                return true; // Already a member
            }

            // Add user to room
            var member = new RoomMember
            {
                UserId = userId,
                RoomId = roomId,
                Role = UserRole.Member,
                JoinedAt = DateTime.UtcNow
            };

            room.Members ??= new List<RoomMember>();
            room.Members.Add(member);

            // Update room
            var roomKey = $"room:{tenantId}:{roomId}";
            await _redisService.SetAsync(roomKey, room, TimeSpan.FromDays(365));

            // Add room to user's room list
            var userRoomsKey = $"user_rooms:{tenantId}:{userId}";
            await _redisService.SetAddAsync(userRoomsKey, roomId);

            _logger.LogInformation("Member added to room successfully. UserId: {UserId}, RoomId: {RoomId}, AddedBy: {AddedBy}", userId, roomId, addedBy);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding member to room. UserId: {UserId}, RoomId: {RoomId}, AddedBy: {AddedBy}", userId, roomId, addedBy);
            return false;
        }
    }

    public async Task<bool> RemoveMemberFromRoomAsync(string roomId, string userId, string removedBy, string tenantId)
    {
        try
        {
            var room = await GetRoomAsync(roomId, tenantId);
            if (room == null || room.CreatedBy != removedBy)
            {
                return false;
            }

            // Remove user from room
            if (room.Members != null)
            {
                var member = room.Members.FirstOrDefault(m => m.UserId == userId);
                if (member != null)
                {
                    room.Members.Remove(member);
                }
            }

            // Update room
            var roomKey = $"room:{tenantId}:{roomId}";
            await _redisService.SetAsync(roomKey, room, TimeSpan.FromDays(365));

            // Remove room from user's room list
            var userRoomsKey = $"user_rooms:{tenantId}:{userId}";
            await _redisService.SetRemoveAsync(userRoomsKey, roomId);

            _logger.LogInformation("Member removed from room successfully. UserId: {UserId}, RoomId: {RoomId}, RemovedBy: {RemovedBy}", userId, roomId, removedBy);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing member from room. UserId: {UserId}, RoomId: {RoomId}, RemovedBy: {RemovedBy}", userId, roomId, removedBy);
            return false;
        }
    }

    public async Task<bool> UpdateMemberRoleAsync(string roomId, string userId, UserRole newRole, string updatedBy, string tenantId)
    {
        try
        {
            var room = await GetRoomAsync(roomId, tenantId);
            if (room == null || room.CreatedBy != updatedBy)
            {
                return false;
            }

            // Update member role
            if (room.Members != null)
            {
                var member = room.Members.FirstOrDefault(m => m.UserId == userId);
                if (member != null)
                {
                    member.Role = newRole;
                    member.UpdatedAt = DateTime.UtcNow;
                }
            }

            // Update room
            var roomKey = $"room:{tenantId}:{roomId}";
            await _redisService.SetAsync(roomKey, room, TimeSpan.FromDays(365));

            _logger.LogInformation("Member role updated successfully. UserId: {UserId}, RoomId: {RoomId}, NewRole: {NewRole}", userId, roomId, newRole);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating member role. UserId: {UserId}, RoomId: {RoomId}, NewRole: {NewRole}", userId, roomId, newRole);
            return false;
        }
    }

    public async Task<bool> UpdateTypingAsync(string roomId, string userId, bool isTyping, string tenantId)
    {
        try
        {
            var typingKey = $"typing:{tenantId}:{roomId}:{userId}";
            
            if (isTyping)
            {
                await _redisService.SetAsync(typingKey, DateTime.UtcNow, TimeSpan.FromSeconds(10));
            }
            else
            {
                await _redisService.DeleteAsync(typingKey);
            }

            // Publish typing update
            var channel = $"room:{tenantId}:{roomId}";
            await _redisService.PublishAsync(channel, $"typing:{userId}:{isTyping}");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating typing status. UserId: {UserId}, RoomId: {RoomId}, IsTyping: {IsTyping}", userId, roomId, isTyping);
            return false;
        }
    }

    public async Task<List<TypingIndicator>> GetTypingIndicatorsAsync(string roomId, string tenantId)
    {
        try
        {
            var typingPattern = $"typing:{tenantId}:{roomId}:*";
            var keys = await _redisService.GetKeysAsync(typingPattern);
            
            var indicators = new List<TypingIndicator>();
            foreach (var key in keys)
            {
                var userId = key.Split(':').Last();
                var timestamp = await _redisService.GetAsync<DateTime>(key);
                
                if (timestamp != default)
                {
                    indicators.Add(new TypingIndicator
                    {
                        UserId = userId,
                        RoomId = roomId,
                        IsTyping = true,
                        Timestamp = timestamp
                    });
                }
            }

            return indicators;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting typing indicators. RoomId: {RoomId}, TenantId: {TenantId}", roomId, tenantId);
            throw;
        }
    }

    public async Task<bool> UpdatePresenceAsync(string userId, PresenceStatus status, string? customStatus, string tenantId)
    {
        try
        {
            var presenceKey = $"presence:{tenantId}:{userId}";
            var presence = new PresenceUpdate
            {
                UserId = userId,
                Status = status,
                CustomStatus = customStatus,
                LastSeen = DateTime.UtcNow
            };

            await _redisService.SetAsync(presenceKey, presence, TimeSpan.FromMinutes(5));

            // Publish presence update
            var channel = $"presence:{tenantId}";
            await _redisService.PublishAsync(channel, $"presence:{userId}:{status}");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating presence. UserId: {UserId}, Status: {Status}", userId, status);
            return false;
        }
    }

    public async Task<PresenceUpdate> GetUserPresenceAsync(string userId, string tenantId)
    {
        try
        {
            var presenceKey = $"presence:{tenantId}:{userId}";
            var presence = await _redisService.GetAsync<PresenceUpdate>(presenceKey);
            
            if (presence == null)
            {
                return new PresenceUpdate
                {
                    UserId = userId,
                    Status = PresenceStatus.Offline,
                    LastSeen = DateTime.UtcNow
                };
            }

            return presence;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user presence. UserId: {UserId}, TenantId: {TenantId}", userId, tenantId);
            throw;
        }
    }

    public async Task<List<PresenceUpdate>> GetRoomPresenceAsync(string roomId, string tenantId)
    {
        try
        {
            var room = await GetRoomAsync(roomId, tenantId);
            if (room?.Members == null)
            {
                return new List<PresenceUpdate>();
            }

            var presenceList = new List<PresenceUpdate>();
            foreach (var member in room.Members)
            {
                var presence = await GetUserPresenceAsync(member.UserId, tenantId);
                presenceList.Add(presence);
            }

            return presenceList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting room presence. RoomId: {RoomId}, TenantId: {TenantId}", roomId, tenantId);
            throw;
        }
    }

    public async Task<bool> MarkMessageAsReadAsync(string messageId, string userId, string roomId, string tenantId)
    {
        try
        {
            var readReceipt = new ReadReceipt
            {
                MessageId = messageId,
                UserId = userId,
                RoomId = roomId,
                ReadAt = DateTime.UtcNow
            };

            var receiptKey = $"read_receipt:{tenantId}:{messageId}:{userId}";
            await _redisService.SetAsync(receiptKey, readReceipt, TimeSpan.FromDays(30));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking message as read. MessageId: {MessageId}, UserId: {UserId}", messageId, userId);
            return false;
        }
    }

    public async Task<List<ReadReceipt>> GetMessageReadReceiptsAsync(string messageId, string tenantId)
    {
        try
        {
            var receiptPattern = $"read_receipt:{tenantId}:{messageId}:*";
            var keys = await _redisService.GetKeysAsync(receiptPattern);
            
            var receipts = new List<ReadReceipt>();
            foreach (var key in keys)
            {
                var receipt = await _redisService.GetAsync<ReadReceipt>(key);
                if (receipt != null)
                {
                    receipts.Add(receipt);
                }
            }

            return receipts.OrderByDescending(r => r.ReadAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting message read receipts. MessageId: {MessageId}, TenantId: {TenantId}", messageId, tenantId);
            throw;
        }
    }

    public async Task<HealthCheckResponse> HealthCheckAsync()
    {
        try
        {
            var redisHealth = await _redisService.GetHealthAsync();
            
            return new HealthCheckResponse
            {
                IsHealthy = redisHealth.IsHealthy,
                Status = redisHealth.IsHealthy ? "Healthy" : "Unhealthy",
                Details = new Dictionary<string, object>
                {
                    ["Redis"] = redisHealth.IsHealthy ? "Connected" : "Disconnected",
                    ["Timestamp"] = DateTime.UtcNow
                },
                CheckedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return new HealthCheckResponse
            {
                IsHealthy = false,
                Status = "Unhealthy",
                Details = new Dictionary<string, object>
                {
                    ["Error"] = ex.Message,
                    ["Timestamp"] = DateTime.UtcNow
                },
                CheckedAt = DateTime.UtcNow
            };
        }
    }
}

public class ChatServiceOptions
{
    public int MaxMessageLength { get; set; } = 10000;
    public int MaxRoomMembers { get; set; } = 1000;
    public TimeSpan MessageRetention { get; set; } = TimeSpan.FromDays(30);
}
