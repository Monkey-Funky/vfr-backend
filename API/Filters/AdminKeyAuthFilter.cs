namespace API.Filters;

/// <summary>
/// Action filter that enforces admin-only access via a pre-shared secret key.
/// The caller must supply the key in the X-Admin-Key request header.
/// The expected key is read from configuration at "Admin:SecretKey".
/// Returns 401 if the header is missing or the key is wrong.
/// </summary>
public sealed class AdminKeyAuthFilter : IActionFilter
{
    private const string HeaderName = "X-Admin-Key";
    private readonly string _expectedKey;

    public AdminKeyAuthFilter(IConfiguration configuration)
    {
        _expectedKey = configuration["Admin:SecretKey"] ?? string.Empty;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (string.IsNullOrWhiteSpace(_expectedKey))
        {
            context.Result = new ObjectResult(new ApiErrorResponse
            {
                Code = "ADMIN_NOT_CONFIGURED",
                Message = "Admin secret key is not configured on this server.",
                TraceId = context.HttpContext.TraceIdentifier
            })
            { StatusCode = StatusCodes.Status503ServiceUnavailable };
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var suppliedKey)
            || !string.Equals(suppliedKey, _expectedKey, StringComparison.Ordinal))
        {
            context.Result = new ObjectResult(new ApiErrorResponse
            {
                Code = "UNAUTHORIZED",
                Message = "Valid admin key required.",
                TraceId = context.HttpContext.TraceIdentifier
            })
            { StatusCode = StatusCodes.Status401Unauthorized };
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
