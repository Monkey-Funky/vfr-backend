namespace API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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

    private static (int StatusCode, ApiErrorResponse Response) MapException(
        Exception exception,
        string traceId)
    {
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

            UnauthorizedException ex => (
                (int)HttpStatusCode.Forbidden,
                new ApiErrorResponse
                {
                    Code = "FORBIDDEN",
                    Message = ex.Message,
                    TraceId = traceId
                }),

            ExternalServiceException ex => (
                (int)HttpStatusCode.BadGateway,
                new ApiErrorResponse
                {
                    Code = "EXTERNAL_SERVICE_ERROR",
                    Message = $"External service '{ex.ServiceName}' is unavailable. Please try again.",
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

            Microsoft.EntityFrameworkCore.DbUpdateException dbEx 
                when dbEx.InnerException is Npgsql.PostgresException { SqlState: "23505" } => (
                (int)HttpStatusCode.Conflict,
                new ApiErrorResponse
                {
                    Code = "CONFLICT",
                    Message = "A resource with the same unique constraint already exists.",
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

            _ => (
                (int)HttpStatusCode.InternalServerError,
                new ApiErrorResponse
                {
                    Code = "INTERNAL_ERROR",
                    Message = "An unexpected error occurred.",
                    TraceId = traceId
                })
        };
    }
}