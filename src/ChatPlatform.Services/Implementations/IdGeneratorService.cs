using ChatPlatform.Core.Interfaces;

namespace ChatPlatform.Services.Implementations;

public class IdGeneratorService : IIdGenerator
{
    private readonly object _lockObject = new();
    private long _counter = 0;
    
    public string GenerateId()
    {
        return GenerateId(null);
    }
    
    public string GenerateId(string? prefix = null)
    {
        lock (_lockObject)
        {
            _counter++;
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var random = Random.Shared.Next(1000, 9999);
            var id = $"{timestamp}{_counter:D6}{random:D4}";
            
            return string.IsNullOrEmpty(prefix) ? id : $"{prefix}_{id}";
        }
    }
    
    public string GenerateIdWithFormat(string format)
    {
        if (string.IsNullOrEmpty(format))
            return GenerateId();
            
        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var random = Random.Shared.Next(1000, 9999);
            
            return format
                .Replace("{timestamp}", timestamp.ToString())
                .Replace("{random}", random.ToString("D4"))
                .Replace("{guid}", Guid.NewGuid().ToString("N")[..8]);
        }
        catch
        {
            // Fallback to default format if custom format fails
            return GenerateId();
        }
    }
}
