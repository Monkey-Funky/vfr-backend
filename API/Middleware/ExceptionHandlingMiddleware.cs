namespace API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception,
            "Unhandled exception | TraceId: {TraceId} | Error: {Message}",
            context.TraceIdentifier, exception.Message);

        var (statusCode, response) = MapException(exception, context.TraceIdentifier);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }

    private (int StatusCode, ApiErrorResponse Response) MapException(
        Exception exception,
        string traceId)
    {
        var isProduction = _environment.IsProduction();

        return exception switch
        {
            ValidationException ex => (
                (int)HttpStatusCode.UnprocessableContent,
                new ApiErrorResponse
                {
                    Code = "VALIDATION_ERROR",
                    Message = ex.Message,
                    Details = ex.Errors.SelectMany(e => e.Value),
                    TraceId = traceId
                }),

            BusinessRuleException ex => (
                (int)HttpStatusCode.UnprocessableContent,
                new ApiErrorResponse
                {
                    Code = ex.Code,
                    Message = ex.Message,
                    TraceId = traceId
                }),

            NotFoundException ex => (
                (int)HttpStatusCode.NotFound,
                new ApiErrorResponse
                {
                    Code = "NOT_FOUND",
                    Message = ex.Message,
                    TraceId = traceId
                }),

            ConflictException ex => (
                (int)HttpStatusCode.Conflict,
                new ApiErrorResponse
                {
                    Code = "CONFLICT",
                    Message = ex.Message,
                    TraceId = traceId
                }),

            // 401 — authentication failures (invalid credentials / expired or malformed tokens).
            AuthenticationException ex => (
                (int)HttpStatusCode.Unauthorized,
                new ApiErrorResponse
                {
                    Code = "UNAUTHORIZED",
                    Message = ex.Message,
                    TraceId = traceId
                }),

            // 403 — IDOR / access-control violations.
            UnauthorizedException ex => (
                (int)HttpStatusCode.Forbidden,
                new ApiErrorResponse
                {
                    Code = "FORBIDDEN",
                    Message = ex.Message,
                    TraceId = traceId
                }),

            // 502 — fal.ai or other external service errors.
            // ex.Message already contains the HTTP status + response body from the service.
            ExternalServiceException ex => (
                (int)HttpStatusCode.BadGateway,
                new ApiErrorResponse
                {
                    Code = "EXTERNAL_SERVICE_ERROR",
                    Message = ex.Message,
                    TraceId = traceId
                }),

            UnauthorizedAccessException => (
                (int)HttpStatusCode.Unauthorized,
                new ApiErrorResponse
                {
                    Code = "UNAUTHORIZED",
                    Message = "Authentication is required.",
                    TraceId = traceId
                }),

            // Unique-constraint violation (e.g. duplicate email).
            Microsoft.EntityFrameworkCore.DbUpdateException dbEx
                when dbEx.InnerException is Npgsql.PostgresException { SqlState: "23505" } => (
                (int)HttpStatusCode.Conflict,
                new ApiErrorResponse
                {
                    Code = "CONFLICT",
                    Message = "A resource with the same unique constraint already exists.",
                    TraceId = traceId
                }),

            // Other DB errors — surface SqlState + detail outside production so the root cause
            // is visible without having to pull server logs.
            Microsoft.EntityFrameworkCore.DbUpdateException dbEx => (
                (int)HttpStatusCode.InternalServerError,
                new ApiErrorResponse
                {
                    Code = "DATABASE_ERROR",
                    Message = isProduction
                        ? "A database error occurred. Please try again."
                        : $"[DbUpdateException] {dbEx.InnerException?.Message ?? dbEx.Message}",
                    TraceId = traceId
                }),

            // Raw Postgres exception not wrapped in DbUpdateException.
            Npgsql.PostgresException pgEx => (
                (int)HttpStatusCode.InternalServerError,
                new ApiErrorResponse
                {
                    Code = "DATABASE_ERROR",
                    Message = isProduction
                        ? "A database error occurred. Please try again."
                        : $"[PostgreSQL {pgEx.SqlState}] {pgEx.MessageText}",
                    TraceId = traceId
                }),

            Polly.Timeout.TimeoutRejectedException => (
                (int)HttpStatusCode.GatewayTimeout,
                new ApiErrorResponse
                {
                    Code = "SERVICE_TIMEOUT",
                    Message = "The try-on service did not respond in time. Please try again.",
                    TraceId = traceId
                }),

            Polly.CircuitBreaker.BrokenCircuitException => (
                (int)HttpStatusCode.ServiceUnavailable,
                new ApiErrorResponse
                {
                    Code = "SERVICE_UNAVAILABLE",
                    Message = "The try-on service is temporarily unavailable. Please try again later.",
                    TraceId = traceId
                }),

            // Catch-all: in non-production expose the exception type and message so the
            // root cause is visible without pulling server logs.
            _ => (
                (int)HttpStatusCode.InternalServerError,
                new ApiErrorResponse
                {
                    Code = "INTERNAL_ERROR",
                    Message = isProduction
                        ? "An unexpected error occurred."
                        : $"[{exception.GetType().Name}] {exception.Message}",
                    Details = isProduction
                        ? []
                        : [exception.InnerException is { } inner
                            ? $"Inner: [{inner.GetType().Name}] {inner.Message}"
                            : exception.StackTrace?.Split('\n').FirstOrDefault()?.Trim() ?? ""],
                    TraceId = traceId
                })
        };
    }
}
