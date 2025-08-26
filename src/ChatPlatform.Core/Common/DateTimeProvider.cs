using ChatPlatform.Core.Interfaces;

namespace ChatPlatform.Core.Common;

public class DateTimeProvider : IDateTimeProvider
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

public class FixedDateTimeProvider : IDateTimeProvider
{
    private readonly DateTime _fixedUtcNow;
    private readonly DateTime _fixedNow;
    private readonly TimeZoneInfo _timeZone;

    public FixedDateTimeProvider(DateTime fixedUtcNow, TimeZoneInfo? timeZone = null)
    {
        _fixedUtcNow = fixedUtcNow;
        _timeZone = timeZone ?? TimeZoneInfo.Utc;
        _fixedNow = TimeZoneInfo.ConvertTimeFromUtc(fixedUtcNow, _timeZone);
    }

    public DateTime UtcNow => _fixedUtcNow;
    public DateTime Now => _fixedNow;
    public DateTime Today => _fixedNow.Date;
    
    public DateTime ConvertFromUtc(DateTime utcDateTime, string destinationTimeZone)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(destinationTimeZone);
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
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
            return localDateTime;
        }
    }
}

public static class DateTimeExtensions
{
    public static bool IsExpired(this DateTime dateTime, IDateTimeProvider dateTimeProvider)
    {
        return dateTime < dateTimeProvider.UtcNow;
    }

    public static TimeSpan GetAge(this DateTime dateTime, IDateTimeProvider dateTimeProvider)
    {
        return dateTimeProvider.UtcNow - dateTime;
    }

    public static bool IsRecent(this DateTime dateTime, TimeSpan threshold, IDateTimeProvider dateTimeProvider)
    {
        return dateTime.GetAge(dateTimeProvider) <= threshold;
    }
}
