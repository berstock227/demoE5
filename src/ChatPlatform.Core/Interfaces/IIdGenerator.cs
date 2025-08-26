namespace ChatPlatform.Core.Interfaces;

public interface IIdGenerator
{
    /// <summary>
    /// Generates a new unique identifier
    /// </summary>
    /// <returns>A unique string identifier</returns>
    string GenerateId();
    
    /// <summary>
    /// Generates a new unique identifier with optional prefix
    /// </summary>
    /// <param name="prefix">Optional prefix for the generated ID</param>
    /// <returns>A unique string identifier with optional prefix</returns>
    string GenerateId(string? prefix = null);
    
    /// <summary>
    /// Generates a new unique identifier with specific format
    /// </summary>
    /// <param name="format">Format string for the ID</param>
    /// <returns>A unique string identifier in the specified format</returns>
    string GenerateIdWithFormat(string format);
}
