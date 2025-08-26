namespace ChatPlatform.Core.Models;

public class SendMessageRequest
{
    public string? MessageId { get; set; }
    public string RoomId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public MessageType MessageType { get; set; } = MessageType.Text;
    public List<AttachmentRequest>? Attachments { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}

public class SendMessageResponse
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public Message? Message { get; set; }
}

public class AttachmentRequest
{
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;
}

public class CreateRoomRequest
{
    public string? RoomId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RoomType Type { get; set; } = RoomType.Group;
    
    // Properties that validators need
    public string CreatedBy { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}

public class UpdateRoomRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RoomType Type { get; set; } = RoomType.Group;
}

public class HealthCheckResponse
{
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object> Details { get; set; } = new();
    public DateTime CheckedAt { get; set; }
}
