namespace ChatPlatform.Core.Common;

public interface IIdGenerator
{
    string GenerateMessageId();
    string GenerateRoomId();
    string GenerateUserId();
    string GenerateTenantId();
    string GenerateMemberId();
    string GenerateReceiptId();
    string GenerateAttachmentId();
}

public class IdGenerator : IIdGenerator
{
    private readonly string _prefix;
    private readonly bool _useHyphens;

    public IdGenerator(string prefix = "", bool useHyphens = false)
    {
        _prefix = prefix;
        _useHyphens = useHyphens;
    }

    public string GenerateMessageId() => GenerateId("msg");
    public string GenerateRoomId() => GenerateId("room");
    public string GenerateUserId() => GenerateId("user");
    public string GenerateTenantId() => GenerateId("tenant");
    public string GenerateMemberId() => GenerateId("member");
    public string GenerateReceiptId() => GenerateId("receipt");
    public string GenerateAttachmentId() => GenerateId("att");

    private string GenerateId(string type)
    {
        var guid = Guid.NewGuid();
        var guidString = _useHyphens ? guid.ToString() : guid.ToString("N");
        
        if (string.IsNullOrEmpty(_prefix))
            return $"{type}_{guidString}";
        
        return $"{_prefix}_{type}_{guidString}";
    }
}

public static class IdGeneratorExtensions
{
    public static string GenerateId<T>(this IIdGenerator generator) where T : class
    {
        return typeof(T).Name.ToLowerInvariant() switch
        {
            "message" => generator.GenerateMessageId(),
            "room" => generator.GenerateRoomId(),
            "user" => generator.GenerateUserId(),
            "tenant" => generator.GenerateTenantId(),
            "roommember" => generator.GenerateMemberId(),
            "readreceipt" => generator.GenerateReceiptId(),
            "attachment" => generator.GenerateAttachmentId(),
            _ => throw new ArgumentException($"Unknown type: {typeof(T).Name}")
        };
    }
}
