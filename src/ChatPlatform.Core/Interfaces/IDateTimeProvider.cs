namespace ChatPlatform.Core.Interfaces;

public interface IDateTimeProvider
{
    /// <summary>
    /// Gets the current UTC date and time
    /// </summary>
    DateTime UtcNow { get; }
    
    /// <summary>
    /// Gets the current local date and time
    /// </summary>
    DateTime Now { get; }
    
    /// <summary>
    /// Gets today's date
    /// </summary>
    DateTime Today { get; }
    
    /// <summary>
    /// Converts UTC time to a specific timezone
    /// </summary>
    /// <param name="utcDateTime">UTC datetime to convert</param>
    /// <param name="destinationTimeZone">Target timezone</param>
    /// <returns>Converted datetime in the target timezone</returns>
    DateTime ConvertFromUtc(DateTime utcDateTime, string destinationTimeZone);
    
    /// <summary>
    /// Converts local time to UTC
    /// </summary>
    /// <param name="localDateTime">Local datetime to convert</param>
    /// <returns>UTC datetime</returns>
    DateTime ConvertToUtc(DateTime localDateTime);
}
