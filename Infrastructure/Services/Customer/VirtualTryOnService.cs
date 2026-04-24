using Application.Features.Customer.VirtualTryOn.DTOs;
using Application.Interfaces.Services.Customer;
using Domain.Entities.Customer;
using Domain.Enums.Customer;
using Polly.Registry;

namespace Infrastructure.Services.Customer;

public sealed class VirtualTryOnService : IVirtualTryOnService
{
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;
    private readonly ILogger<VirtualTryOnService> _logger;

    public VirtualTryOnService(
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<VirtualTryOnService> logger)
    {
        _pipelineProvider = pipelineProvider;
        _logger = logger;
    }

    public async Task<TryOnResultDto> ProcessTryOnAsync(
        Guid customerId, 
        Guid productId, 
        TryOnSessionType sessionType, 
        Avatar? avatar, 
        CancellationToken ct)
    {
        var pipeline = _pipelineProvider.GetPipeline("tryon");

        return await pipeline.ExecuteAsync(async cancellationToken =>
        {
            _logger.LogInformation("Processing Virtual Try-On for Customer {CustomerId} and Product {ProductId}", customerId, productId);
            
            try
            {
                // Placeholder: Simulate an external ML inference process
                await Task.Delay(2000, cancellationToken);

                // In a real scenario, this would call Python/ML APIs. We simply mock success.
                return new TryOnResultDto(
                    Status: SessionStatus.Completed,
                    ResultImageUrl: $"https://cdn.vfr.app/tryon/{Guid.NewGuid()}.jpg",
                    RecommendedSize: "M",
                    ConfidenceScore: 0.95m,
                    DurationSeconds: 2
                );
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Virtual Try-On process failed.");
                throw new ExternalServiceException("VirtualTryOn", "The Try-On service encountered an error.");
            }
        }, ct);
    }
}
