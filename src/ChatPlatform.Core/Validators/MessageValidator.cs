using ChatPlatform.Core.Common;
using ChatPlatform.Core.Models;

namespace ChatPlatform.Core.Validators;

public interface IMessageValidator
{
    ValidationResult ValidateSendMessage(SendMessageRequest request);
    ValidationResult ValidateMessageUpdate(string messageId, string content, string userId, string tenantId);
    ValidationResult ValidateMessageDeletion(string messageId, string userId, string tenantId);
}

public class MessageValidator : IMessageValidator
{
    public ValidationResult ValidateSendMessage(SendMessageRequest request)
    {
        var result = new ValidationResult();

        // Validate required fields
        var contentValidation = request.Content.ValidateNotEmpty("Content");
        var roomIdValidation = request.RoomId.ValidateNotEmpty("RoomId");
        var senderIdValidation = request.SenderId.ValidateNotEmpty("SenderId");
        var tenantIdValidation = request.TenantId.ValidateNotEmpty("TenantId");

        if (!contentValidation.IsValid) result.AddError("Content", contentValidation.ErrorMessage ?? "Content is required");
        if (!roomIdValidation.IsValid) result.AddError("RoomId", roomIdValidation.ErrorMessage ?? "RoomId is required");
        if (!senderIdValidation.IsValid) result.AddError("SenderId", senderIdValidation.ErrorMessage ?? "SenderId is required");
        if (!tenantIdValidation.IsValid) result.AddError("TenantId", tenantIdValidation.ErrorMessage ?? "TenantId is required");

        // Validate content length
        if (result.IsValid)
        {
            var lengthValidation = request.Content.ValidateLength("Content", 1, 10000, "Message content must be between 1 and 10,000 characters");
            if (!lengthValidation.IsValid)
            {
                result.AddError("Content", lengthValidation.ErrorMessage ?? "Invalid content length");
            }
        }

        // Validate message type
        if (result.IsValid && !Enum.IsDefined(typeof(MessageType), request.MessageType))
        {
            result.AddError("MessageType", "Invalid message type");
        }

        return result;
    }

    public ValidationResult ValidateMessageUpdate(string messageId, string content, string userId, string tenantId)
    {
        var result = new ValidationResult();

        var messageIdValidation = messageId.ValidateNotEmpty("MessageId");
        var contentValidation = content.ValidateNotEmpty("Content");
        var userIdValidation = userId.ValidateNotEmpty("UserId");
        var tenantIdValidation = tenantId.ValidateNotEmpty("TenantId");

        if (!messageIdValidation.IsValid) result.AddError("MessageId", messageIdValidation.ErrorMessage ?? "MessageId is required");
        if (!contentValidation.IsValid) result.AddError("Content", contentValidation.ErrorMessage ?? "Content is required");
        if (!userIdValidation.IsValid) result.AddError("UserId", userIdValidation.ErrorMessage ?? "UserId is required");
        if (!tenantIdValidation.IsValid) result.AddError("TenantId", tenantIdValidation.ErrorMessage ?? "TenantId is required");

        if (result.IsValid)
        {
            var guidValidation = messageId.ValidateGuid("MessageId");
            var lengthValidation = content.ValidateLength("Content", 1, 10000);

            if (!guidValidation.IsValid) result.AddError("MessageId", guidValidation.ErrorMessage ?? "MessageId must be a valid GUID");
            if (!lengthValidation.IsValid) result.AddError("Content", lengthValidation.ErrorMessage ?? "Invalid content length");
        }

        return result;
    }

    public ValidationResult ValidateMessageDeletion(string messageId, string userId, string tenantId)
    {
        var result = new ValidationResult();

        var messageIdValidation = messageId.ValidateNotEmpty("MessageId");
        var userIdValidation = userId.ValidateNotEmpty("UserId");
        var tenantIdValidation = tenantId.ValidateNotEmpty("TenantId");

        if (!messageIdValidation.IsValid) result.AddError("MessageId", messageIdValidation.ErrorMessage ?? "MessageId is required");
        if (!userIdValidation.IsValid) result.AddError("UserId", userIdValidation.ErrorMessage ?? "UserId is required");
        if (!tenantIdValidation.IsValid) result.AddError("TenantId", tenantIdValidation.ErrorMessage ?? "TenantId is required");

        if (result.IsValid)
        {
            var guidValidation = messageId.ValidateGuid("MessageId");
            if (!guidValidation.IsValid)
            {
                result.AddError("MessageId", guidValidation.ErrorMessage ?? "MessageId must be a valid GUID");
            }
        }

        return result;
    }
}
