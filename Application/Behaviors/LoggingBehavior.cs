namespace Application.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUserService;

    public LoggingBehavior(
        ILogger<LoggingBehavior<TRequest, TResponse>> logger,
        ICurrentUserService currentUserService)
    {
        _logger = logger;
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _currentUserService.UserId ?? "Anonymous";

        _logger.LogInformation(
            "Processing request: {RequestName} by User: {UserId} at {DateTime}",
            requestName, userId, DateTime.UtcNow);

        try
        {
            var response = await next();

            _logger.LogInformation(
                "Completed request: {RequestName} by User: {UserId}",
                requestName, userId);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Request failed: {RequestName} by User: {UserId}. Error: {ErrorMessage}",
                requestName, userId, ex.Message);
            throw;
        }
    }
}
