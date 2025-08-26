using ChatPlatform.Core.Interfaces;
using ChatPlatform.Core.Models;
using ChatPlatform.Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace ChatPlatform.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
               User.FindFirst("sub")?.Value ?? 
               throw new UnauthorizedAccessException("User ID not found in token");
    }

    private string GetCurrentTenantId()
    {
        return "demo-tenant"; // Sử dụng cùng tenant cho tất cả users để test
    }

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var tenantId = GetCurrentTenantId();

            var message = new Message
            {
                Id = request.MessageId ?? Guid.NewGuid().ToString(),
                RoomId = request.RoomId,
                SenderId = userId,
                TenantId = tenantId,
                Content = request.Content,
                Type = request.MessageType,
                Attachments = new List<Attachment>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await _chatService.SendMessageAsync(new ChatPlatform.Core.Models.SendMessageRequest
            {
                MessageId = message.Id,
                RoomId = message.RoomId,
                Content = message.Content,
                MessageType = message.Type,
                SenderId = userId,
                TenantId = tenantId,
                Attachments = new List<ChatPlatform.Core.Models.AttachmentRequest>()
            });
            
            if (result?.Success == true)
            {
                return Ok(new { MessageId = result.MessageId, Success = true });
            }

            return BadRequest(new { Error = result?.ErrorMessage ?? "Failed to send message" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    [HttpGet("rooms/{roomId}/messages")]
    public async Task<IActionResult> GetRoomMessages(string roomId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            var tenantId = GetCurrentTenantId();
            var offset = (page - 1) * pageSize;
            var result = await _chatService.GetRoomMessagesAsync(roomId, tenantId, pageSize, offset);
            
            if (result != null && result.Any())
            {
                return Ok(result);
            }

            return Ok(new List<object>()); // Return empty list instead of NotFound
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting room messages");
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    [HttpPost("rooms")]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var tenantId = GetCurrentTenantId();

            var roomId = Guid.NewGuid().ToString();
            var room = new Room
            {
                Id = roomId,
                Name = request.Name,
                Description = request.Description,
                Type = request.Type,
                TenantId = tenantId,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Members = new List<RoomMember>
                {
                    new RoomMember
                    {
                        RoomId = roomId,
                        UserId = userId,
                        Role = UserRole.Admin,
                        JoinedAt = DateTime.UtcNow
                    }
                }
            };

            var result = await _chatService.CreateRoomAsync(new ChatPlatform.Core.Models.CreateRoomRequest
            {
                Name = room.Name,
                Description = room.Description,
                Type = room.Type,
                TenantId = tenantId,
                CreatedBy = userId
            });
            
            if (result != null)
            {
                // Return the created room object for immediate display
                return Ok(new { 
                    RoomId = result.Id, 
                    Success = true,
                    Room = new {
                        Id = result.Id,
                        Name = result.Name,
                        Description = result.Description,
                        Type = result.Type
                    }
                });
            }

            return BadRequest(new { Error = "Failed to create room" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating room");
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    [HttpGet("rooms")]
    public async Task<IActionResult> GetRooms()
    {
        try
        {
            var tenantId = GetCurrentTenantId();
            // Lấy tất cả rooms trong tenant, không chỉ rooms của user
            var rooms = await _chatService.GetAllRoomsAsync(tenantId);
            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rooms");
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }



    [HttpGet("rooms/user")]
    public async Task<IActionResult> GetUserRooms()
    {
        try
        {
            var userId = GetCurrentUserId();
            var tenantId = GetCurrentTenantId();

            var result = await _chatService.GetUserRoomsAsync(userId, tenantId);
            
            if (result != null && result.Any())
            {
                return Ok(result);
            }

            return NotFound(new { Error = "No rooms found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user rooms");
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    [HttpPost("rooms/{roomId}/join")]
    public async Task<IActionResult> JoinRoom(string roomId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var tenantId = GetCurrentTenantId();

            var result = await _chatService.JoinRoomAsync(roomId, userId, tenantId);
            
            if (result)
            {
                return Ok(new { Success = true });
            }

            return BadRequest(new { Error = "Failed to join room" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining room");
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    [HttpPost("rooms/{roomId}/leave")]
    public async Task<IActionResult> LeaveRoom(string roomId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var tenantId = GetCurrentTenantId();

            var result = await _chatService.LeaveRoomAsync(roomId, userId, tenantId);
            
            if (result)
            {
                return Ok(new { Success = true });
            }

            return BadRequest(new { Error = "Failed to leave room" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving room");
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<IActionResult> HealthCheck()
    {
        try
        {
            var result = await _chatService.HealthCheckAsync();
            return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(503, new { Status = "Unhealthy", Error = ex.Message });
        }
    }
}

// DTO classes
public class CreateRoomRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    public RoomType Type { get; set; }
}

public class SendMessageRequest
{
    public string? MessageId { get; set; }
    
    [Required]
    public string RoomId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(10000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
    
    public MessageType MessageType { get; set; }
    
    public List<AttachmentRequest> Attachments { get; set; } = new();
}

public class AttachmentRequest
{
    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    [Url]
    public string FileUrl { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string ContentType { get; set; } = string.Empty;
    
    [Range(1, long.MaxValue)]
    public long FileSize { get; set; }
}


