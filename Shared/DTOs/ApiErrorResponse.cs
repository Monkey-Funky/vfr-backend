namespace Shared.DTOs;

/// <summary>
/// Structured error envelope returned by ExceptionHandlingMiddleware.
/// Consistent shape regardless of exception type.
/// </summary>
public sealed class ApiErrorResponse
{
    public bool Success => false;
    public string Code { get; init; } = "INTERNAL_ERROR";
    public string Message { get; init; } = string.Empty;
    public IEnumerable<string> Details { get; init; } = [];
    public string? TraceId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}