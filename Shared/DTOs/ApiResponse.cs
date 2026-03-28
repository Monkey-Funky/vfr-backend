namespace Shared.DTOs;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IEnumerable<string> Errors { get; init; } = [];
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? TraceId { get; init; }

    public static ApiResponse<T> SuccessResponse(T data, string message = "Request successful")
        => new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> FailureResponse(string message, IEnumerable<string>? errors = null)
        => new() { Success = false, Message = message, Errors = errors ?? [] };

    public static ApiResponse<T> FromResult(Result<T> result)
        => new()
        {
            Success = result.IsSuccess,
            Message = result.Message,
            Data = result.Data,
            Errors = result.Errors
        };
}