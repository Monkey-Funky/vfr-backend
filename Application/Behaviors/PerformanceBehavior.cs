using Application.Interfaces.Services;

namespace Application.Behaviors;

public sealed class PerformanceBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private const int DefaultSlowRequestThresholdMs = 500;

    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly int _slowRequestThresholdMs;

    public PerformanceBehavior(
        ILogger<PerformanceBehavior<TRequest, TResponse>> logger,
        ICurrentUserService currentUserService,
        int slowRequestThresholdMs = DefaultSlowRequestThresholdMs)
    {
        _logger = logger;
        _currentUserService = currentUserService;
        _slowRequestThresholdMs = slowRequestThresholdMs;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();

        var response = await next();

        timer.Stop();

        if (timer.ElapsedMilliseconds > _slowRequestThresholdMs)
        {
            _logger.LogWarning(
                "Slow request detected: {RequestName} | {ElapsedMs}ms | RetailerId: {RetailerId}",
                typeof(TRequest).Name,
                timer.ElapsedMilliseconds,
                _currentUserService.RetailerId?.ToString() ?? "anonymous");
        }

        return response;
    }
}