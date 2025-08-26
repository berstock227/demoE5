namespace ChatPlatform.Core.Common;

public class ValidationResult
{
    public bool IsValid => !Errors.Any();
    public List<ValidationError> Errors { get; } = new();
    
    // Property that validators are expecting
    public string? ErrorMessage => Errors.FirstOrDefault()?.ErrorMessage;

    public void AddError(string propertyName, string errorMessage, string? errorCode = null)
    {
        Errors.Add(new ValidationError(propertyName, errorMessage, errorCode));
    }

    public void AddErrors(IEnumerable<ValidationError> errors)
    {
        Errors.AddRange(errors);
    }

    public static ValidationResult Success() => new();
    
    public static ValidationResult Failure(string propertyName, string errorMessage, string? errorCode = null)
    {
        var result = new ValidationResult();
        result.AddError(propertyName, errorMessage, errorCode);
        return result;
    }

    public static ValidationResult Failure(IEnumerable<ValidationError> errors)
    {
        var result = new ValidationResult();
        result.AddErrors(errors);
        return result;
    }

    public static implicit operator bool(ValidationResult result) => result.IsValid;
}

public class ValidationError
{
    public string PropertyName { get; }
    public string ErrorMessage { get; }
    public string? ErrorCode { get; }

    public ValidationError(string propertyName, string errorMessage, string? errorCode = null)
    {
        PropertyName = propertyName;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }
}
