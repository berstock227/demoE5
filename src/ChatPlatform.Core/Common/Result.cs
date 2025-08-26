using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChatPlatform.Core.Common;

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }

    internal Result(bool isSuccess, T? value = default, string? errorMessage = null, Exception? exception = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    public static Result<T> Success(T value) => new(true, value);
    public static Result<T> Failure(string errorMessage) => new(false, errorMessage: errorMessage);
    public static Result<T> Failure(string errorMessage, Exception exception) => new(false, errorMessage: errorMessage, exception: exception);

    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return IsSuccess && Value != null 
            ? Result<TNew>.Success(mapper(Value)) 
            : Result<TNew>.Failure(ErrorMessage ?? "Unknown error");
    }

    public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
    {
        return IsSuccess && Value != null 
            ? Result<TNew>.Success(await mapper(Value)) 
            : Result<TNew>.Failure(ErrorMessage ?? "Unknown error");
    }

    public TNew Match<TNew>(Func<T, TNew> onSuccess, Func<string, TNew> onFailure)
    {
        return IsSuccess && Value != null 
            ? onSuccess(Value) 
            : onFailure(ErrorMessage ?? "Unknown error");
    }

    public async Task<TNew> MatchAsync<TNew>(Func<T, Task<TNew>> onSuccess, Func<string, Task<TNew>> onFailure)
    {
        return IsSuccess && Value != null 
            ? await onSuccess(Value) 
            : await onFailure(ErrorMessage ?? "Unknown error");
    }
}

public class Result : Result<Unit>
{
    internal Result(bool isSuccess, string? errorMessage = null, Exception? exception = null) 
        : base(isSuccess, Unit.Value, errorMessage, exception)
    {
    }

    public static Result Success() => new(true);
    public static new Result Failure(string errorMessage) => new(false, errorMessage);
    public static new Result Failure(string errorMessage, Exception exception) => new(false, errorMessage, exception);
}

public struct Unit
{
    public static Unit Value => default;
}
