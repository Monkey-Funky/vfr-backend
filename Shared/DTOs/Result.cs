namespace Shared.DTOs;

public class Result
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public IEnumerable<string> Errors { get; init; } = [];

    public static Result Success(string message = "Operation completed successfully")
        => new() { IsSuccess = true, Message = message };

    public static Result Failure(string message, IEnumerable<string>? errors = null)
        => new() { IsSuccess = false, Message = message, Errors = errors ?? [] };

    public static Result Failure(string message, string error)
        => new() { IsSuccess = false, Message = message, Errors = [error] };
}

public sealed class Result<T> : Result
{
    public T? Data { get; init; }

    public static Result<T> Success(T data, string message = "Operation completed successfully")
        => new() { IsSuccess = true, Message = message, Data = data };

    public new static Result<T> Failure(string message, IEnumerable<string>? errors = null)
        => new() { IsSuccess = false, Message = message, Errors = errors ?? [] };

    public new static Result<T> Failure(string message, string error)
        => new() { IsSuccess = false, Message = message, Errors = [error] };
}