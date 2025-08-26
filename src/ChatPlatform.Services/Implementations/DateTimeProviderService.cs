using ChatPlatform.Core.Interfaces;

namespace ChatPlatform.Services.Implementations;

public class DateTimeProviderService : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Now => DateTime.Now;
    public DateTime Today => DateTime.Today;
    
    public DateTime ConvertFromUtc(DateTime utcDateTime, string destinationTimeZone)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(destinationTimeZone);
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback to UTC if timezone not found
            return utcDateTime;
        }
    }
    
    public DateTime ConvertToUtc(DateTime localDateTime)
    {
        try
        {
            return localDateTime.ToUniversalTime();
        }
        catch
        {
            // Fallback to original datetime if conversion fails
            return localDateTime;
        }
    }
}
