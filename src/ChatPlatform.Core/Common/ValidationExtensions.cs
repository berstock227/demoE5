using System;
using System.ComponentModel.DataAnnotations;

namespace ChatPlatform.Core.Common;

public static class ValidationExtensions
{
    public static ValidationResult ValidateNotNull<T>(this T? value, string propertyName, string? customMessage = null)
    {
        if (value == null)
        {
            return ValidationResult.Failure(propertyName, customMessage ?? $"{propertyName} cannot be null");
        }
        return ValidationResult.Success();
    }

    public static ValidationResult ValidateNotEmpty(this string? value, string propertyName, string? customMessage = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Failure(propertyName, customMessage ?? $"{propertyName} cannot be empty");
        }
        return ValidationResult.Success();
    }

    public static ValidationResult ValidateLength(this string? value, string propertyName, int minLength, int maxLength, string? customMessage = null)
    {
        if (value == null) return ValidationResult.Success();
        
        if (value.Length < minLength || value.Length > maxLength)
        {
            return ValidationResult.Failure(propertyName, 
                customMessage ?? $"{propertyName} must be between {minLength} and {maxLength} characters");
        }
        return ValidationResult.Success();
    }

    public static ValidationResult ValidateEmail(this string? value, string propertyName, string? customMessage = null)
    {
        if (value == null) return ValidationResult.Success();
        
        try
        {
            var email = new System.Net.Mail.MailAddress(value);
            return ValidationResult.Success();
        }
        catch
        {
            return ValidationResult.Failure(propertyName, customMessage ?? $"{propertyName} must be a valid email address");
        }
    }

    public static ValidationResult ValidateGuid(this string? value, string propertyName, string? customMessage = null)
    {
        if (value == null) return ValidationResult.Success();
        
        if (!Guid.TryParse(value, out _))
        {
            return ValidationResult.Failure(propertyName, customMessage ?? $"{propertyName} must be a valid GUID");
        }
        return ValidationResult.Success();
    }

    public static ValidationResult ValidateRange<T>(this T value, string propertyName, T minValue, T maxValue, string? customMessage = null) where T : IComparable<T>
    {
        if (value.CompareTo(minValue) < 0 || value.CompareTo(maxValue) > 0)
        {
            return ValidationResult.Failure(propertyName, customMessage ?? $"{propertyName} must be between {minValue} and {maxValue}");
        }
        return ValidationResult.Success();
    }

    public static ValidationResult ValidateEnum<T>(this T value, string propertyName, string? customMessage = null) where T : struct, Enum
    {
        if (!Enum.IsDefined(typeof(T), value))
        {
            return ValidationResult.Failure(propertyName, customMessage ?? $"{propertyName} must be a valid {typeof(T).Name} value");
        }
        return ValidationResult.Success();
    }
}
