using ChatPlatform.Core.Common;
using ChatPlatform.Core.Models;

namespace ChatPlatform.Core.Validators;

public interface IRoomValidator
{
    ValidationResult ValidateCreateRoom(CreateRoomRequest request);
    ValidationResult ValidateRoomUpdate(string roomId, UpdateRoomRequest request, string userId, string tenantId);
    ValidationResult ValidateRoomDeletion(string roomId, string userId, string tenantId);
    ValidationResult ValidateRoomMembership(string roomId, string userId, string tenantId);
}

public class RoomValidator : IRoomValidator
{
    public ValidationResult ValidateCreateRoom(CreateRoomRequest request)
    {
        var result = new ValidationResult();

        var nameValidation = request.Name.ValidateNotEmpty("Name");
        var createdByValidation = request.CreatedBy.ValidateNotEmpty("CreatedBy");
        var tenantIdValidation = request.TenantId.ValidateNotEmpty("TenantId");

        if (!nameValidation.IsValid) result.AddError("Name", nameValidation.ErrorMessage ?? "Name is required");
        if (!createdByValidation.IsValid) result.AddError("CreatedBy", createdByValidation.ErrorMessage ?? "CreatedBy is required");
        if (!tenantIdValidation.IsValid) result.AddError("TenantId", tenantIdValidation.ErrorMessage ?? "TenantId is required");

        if (result.IsValid)
        {
            var nameLengthValidation = request.Name.ValidateLength("Name", 1, 100, "Room name must be between 1 and 100 characters");
            var createdByGuidValidation = request.CreatedBy.ValidateGuid("CreatedBy");
            var tenantIdGuidValidation = request.TenantId.ValidateGuid("TenantId");

            if (!nameLengthValidation.IsValid) result.AddError("Name", nameLengthValidation.ErrorMessage ?? "Invalid name length");
            if (!createdByGuidValidation.IsValid) result.AddError("CreatedBy", createdByGuidValidation.ErrorMessage ?? "CreatedBy must be a valid GUID");
            if (!tenantIdGuidValidation.IsValid) result.AddError("TenantId", tenantIdGuidValidation.ErrorMessage ?? "TenantId must be a valid GUID");

            // Validate description if provided
            if (!string.IsNullOrEmpty(request.Description))
            {
                var descLengthValidation = request.Description.ValidateLength("Description", 0, 500, "Room description must be less than 500 characters");
                if (!descLengthValidation.IsValid)
                {
                    result.AddError("Description", descLengthValidation.ErrorMessage ?? "Invalid description length");
                }
            }
        }

        // Validate room type
        if (result.IsValid && !Enum.IsDefined(typeof(RoomType), request.Type))
        {
            result.AddError("Type", "Invalid room type");
        }

        return result;
    }

    public ValidationResult ValidateRoomUpdate(string roomId, UpdateRoomRequest request, string userId, string tenantId)
    {
        var result = new ValidationResult();

        var roomIdValidation = roomId.ValidateNotEmpty("RoomId");
        var userIdValidation = userId.ValidateNotEmpty("UserId");
        var tenantIdValidation = tenantId.ValidateNotEmpty("TenantId");

        if (!roomIdValidation.IsValid) result.AddError("RoomId", roomIdValidation.ErrorMessage ?? "RoomId is required");
        if (!userIdValidation.IsValid) result.AddError("UserId", userIdValidation.ErrorMessage ?? "UserId is required");
        if (!tenantIdValidation.IsValid) result.AddError("TenantId", tenantIdValidation.ErrorMessage ?? "TenantId is required");

        if (result.IsValid)
        {
            var roomIdGuidValidation = roomId.ValidateGuid("RoomId");
            var userIdGuidValidation = userId.ValidateGuid("UserId");
            var tenantIdGuidValidation = tenantId.ValidateGuid("TenantId");

            if (!roomIdGuidValidation.IsValid) result.AddError("RoomId", roomIdGuidValidation.ErrorMessage ?? "RoomId must be a valid GUID");
            if (!userIdGuidValidation.IsValid) result.AddError("UserId", userIdGuidValidation.ErrorMessage ?? "UserId must be a valid GUID");
            if (!tenantIdGuidValidation.IsValid) result.AddError("TenantId", tenantIdGuidValidation.ErrorMessage ?? "TenantId must be a valid GUID");
        }

        // Validate optional fields if provided
        if (result.IsValid)
        {
            if (!string.IsNullOrEmpty(request.Name))
            {
                var nameLengthValidation = request.Name.ValidateLength("Name", 1, 100);
                if (!nameLengthValidation.IsValid)
                {
                    result.AddError("Name", nameLengthValidation.ErrorMessage ?? "Invalid name length");
                }
            }

            if (!string.IsNullOrEmpty(request.Description))
            {
                var descLengthValidation = request.Description.ValidateLength("Description", 0, 500);
                if (!descLengthValidation.IsValid)
                {
                    result.AddError("Description", descLengthValidation.ErrorMessage ?? "Invalid description length");
                }
            }
        }

        return result;
    }

    public ValidationResult ValidateRoomDeletion(string roomId, string userId, string tenantId)
    {
        var result = new ValidationResult();

        var roomIdValidation = roomId.ValidateNotEmpty("RoomId");
        var userIdValidation = userId.ValidateNotEmpty("UserId");
        var tenantIdValidation = tenantId.ValidateNotEmpty("TenantId");

        if (!roomIdValidation.IsValid) result.AddError("RoomId", roomIdValidation.ErrorMessage ?? "RoomId is required");
        if (!userIdValidation.IsValid) result.AddError("UserId", userIdValidation.ErrorMessage ?? "UserId is required");
        if (!tenantIdValidation.IsValid) result.AddError("TenantId", tenantIdValidation.ErrorMessage ?? "TenantId is required");

        if (result.IsValid)
        {
            var roomIdGuidValidation = roomId.ValidateGuid("RoomId");
            var userIdGuidValidation = userId.ValidateGuid("UserId");
            var tenantIdGuidValidation = tenantId.ValidateGuid("TenantId");

            if (!roomIdGuidValidation.IsValid) result.AddError("RoomId", roomIdGuidValidation.ErrorMessage ?? "RoomId must be a valid GUID");
            if (!userIdGuidValidation.IsValid) result.AddError("UserId", userIdGuidValidation.ErrorMessage ?? "UserId must be a valid GUID");
            if (!tenantIdGuidValidation.IsValid) result.AddError("TenantId", tenantIdGuidValidation.ErrorMessage ?? "TenantId must be a valid GUID");
        }

        return result;
    }

    public ValidationResult ValidateRoomMembership(string roomId, string userId, string tenantId)
    {
        var result = new ValidationResult();

        var roomIdValidation = roomId.ValidateNotEmpty("RoomId");
        var userIdValidation = userId.ValidateNotEmpty("UserId");
        var tenantIdValidation = tenantId.ValidateNotEmpty("TenantId");

        if (!roomIdValidation.IsValid) result.AddError("RoomId", roomIdValidation.ErrorMessage ?? "RoomId is required");
        if (!userIdValidation.IsValid) result.AddError("UserId", userIdValidation.ErrorMessage ?? "UserId is required");
        if (!tenantIdValidation.IsValid) result.AddError("TenantId", tenantIdValidation.ErrorMessage ?? "TenantId is required");

        if (result.IsValid)
        {
            var roomIdGuidValidation = roomId.ValidateGuid("RoomId");
            var userIdGuidValidation = userId.ValidateGuid("UserId");
            var tenantIdGuidValidation = tenantId.ValidateGuid("TenantId");

            if (!roomIdGuidValidation.IsValid) result.AddError("RoomId", roomIdGuidValidation.ErrorMessage ?? "RoomId must be a valid GUID");
            if (!userIdGuidValidation.IsValid) result.AddError("UserId", userIdGuidValidation.ErrorMessage ?? "UserId must be a valid GUID");
            if (!tenantIdGuidValidation.IsValid) result.AddError("TenantId", tenantIdGuidValidation.ErrorMessage ?? "TenantId must be a valid GUID");
        }

        return result;
    }
}
