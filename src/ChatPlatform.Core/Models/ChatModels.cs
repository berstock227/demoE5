using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ChatPlatform.Core.Models;

public class Message
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public MessageType Type { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string RoomId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public MessageStatus Status { get; set; }
    public List<Attachment> Attachments { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class Room
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RoomType Type { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public Dictionary<string, object> Settings { get; set; } = new();
    public List<RoomMember> Members { get; set; } = new();
}

public class User
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public bool IsActive { get; set; } = true;
    public Dictionary<string, object> Profile { get; set; } = new();
}

public class RoomMember
{
    public string Id { get; set; } = string.Empty;
    public string RoomId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LastReadAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Attachment
{
    public string Id { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TypingIndicator
{
    public string UserId { get; set; } = string.Empty;
    public string RoomId { get; set; } = string.Empty;
    public bool IsTyping { get; set; }
    public DateTime Timestamp { get; set; }
}

public class PresenceUpdate
{
    public string UserId { get; set; } = string.Empty;
    public PresenceStatus Status { get; set; }
    public string? CustomStatus { get; set; }
    public DateTime LastSeen { get; set; }
}

public class ReadReceipt
{
    public string Id { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string RoomId { get; set; } = string.Empty;
    public DateTime ReadAt { get; set; }
}

public class Tenant
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Dictionary<string, object> Settings { get; set; } = new();
    public Dictionary<string, object> Limits { get; set; } = new();
}

public class ConnectionInfo
{
    public string ConnectionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

// Enums
public enum MessageType
{
    Text,
    Image,
    File,
    Audio,
    Video,
    Location,
    System
}

public enum RoomType
{
    Direct,
    Group,
    Channel,
    Announcement
}

public enum UserRole
{
    User,
    Member,
    Moderator,
    Admin,
    Owner
}

public enum MessageStatus
{
    Sent,
    Delivered,
    Read,
    Failed
}

public enum PresenceStatus
{
    Online,
    Away,
    Busy,
    Offline
}

public enum ReceiptType
{
    Read,
    Delivered,
    Typing
}
