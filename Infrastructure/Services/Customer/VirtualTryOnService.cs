using Application.Features.Customer.VirtualTryOn.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services.Customer;
using Domain.Entities.Customer;
using Domain.Enums.Customer;
using Microsoft.EntityFrameworkCore;
using Polly.Registry;

namespace Infrastructure.Services.Customer;

public sealed class VirtualTryOnService : IVirtualTryOnService
{
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;
    private readonly IFalAiService _falAiService;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<VirtualTryOnService> _logger;

    public VirtualTryOnService(
        ResiliencePipelineProvider<string> pipelineProvider,
        IFalAiService falAiService,
        IApplicationDbContext context,
        ILogger<VirtualTryOnService> logger)
    {
        _pipelineProvider = pipelineProvider;
        _falAiService = falAiService;
        _context = context;
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
            var startTime = DateTime.UtcNow;

            _logger.LogInformation(
                "Processing Virtual Try-On for Customer {CustomerId} and Product {ProductId}",
                customerId, productId);

            // 1. Validate avatar has a 3D model URL
            if (avatar?.Avatar3dModelUrl is null)
            {
                throw new ExternalServiceException("FalAi",
                    "Customer does not have a 3D avatar. Please create one first.");
            }

            // 2. Load product with images
            var product = await _context.Products
                .Include(p => p.Images)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

            if (product is null)
            {
                throw new ExternalServiceException("FalAi", "Product not found.");
            }

            // 3. Get the first non-deleted product image URL
            var productImageUrl = product.Images
                .Where(i => !i.IsDeleted)
                .OrderBy(i => i.DisplayOrder)
                .FirstOrDefault()?.ImageUrl;

            if (string.IsNullOrWhiteSpace(productImageUrl))
            {
                throw new ExternalServiceException("FalAi", "Product has no images for try-on.");
            }

            // 4. Build clothing prompt from product name
            var clothingPrompt = !string.IsNullOrWhiteSpace(product.Name)
                ? product.Name
                : "clothing item";

            _logger.LogInformation(
                "Starting fal.ai 3D try-on pipeline. Product: {ProductName}, BodyMesh: {BodyMeshUrl}",
                clothingPrompt, avatar.Avatar3dModelUrl);

            // 5. Generate 3D model of the clothing item
            var objectGlb = await _falAiService.GenerateObject3dAsync(
                productImageUrl, clothingPrompt, cancellationToken);

            _logger.LogInformation(
                "Clothing 3D model generated. ObjectGlbUrl: {ObjectGlbUrl}",
                objectGlb.GlbUrl);

            // 6. Align body + clothing into one unified scene
            var sceneGlbUrl = await _falAiService.AlignSceneAsync(
                imageUrl: productImageUrl,
                bodyMeshUrl: avatar.Avatar3dModelUrl,
                objectMeshUrl: objectGlb.GlbUrl,
                focalLength: avatar.AvatarFocalLength ?? 1000.0, // use real focal length from SAM 3D Body metadata
                cancellationToken);

            _logger.LogInformation(
                "3D scene alignment completed. SceneGlbUrl: {SceneGlbUrl}",
                sceneGlbUrl);

            // 7. Calculate duration
            var durationSeconds = (int)(DateTime.UtcNow - startTime).TotalSeconds;

            // 8. Return result
            return new TryOnResultDto(
                Status: SessionStatus.Completed,
                ResultImageUrl: sceneGlbUrl,
                RecommendedSize: null,
                ConfidenceScore: 0.98m,
                DurationSeconds: durationSeconds);
        }, ct);
    }
}
