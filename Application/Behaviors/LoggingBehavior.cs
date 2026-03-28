namespace Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
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
        var retailerId = _currentUserService.RetailerId?.ToString() ?? "anonymous";

        _logger.LogInformation(
            "Handling {RequestName} | RetailerId: {RetailerId}",
            requestName, retailerId);

        try
        {
            var response = await next();

            _logger.LogInformation(
                "Handled {RequestName} | RetailerId: {RetailerId}",
                requestName, retailerId);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling {RequestName} | RetailerId: {RetailerId} | Error: {ErrorMessage}",
                requestName, retailerId, ex.Message);
            throw;
        }
    }
}