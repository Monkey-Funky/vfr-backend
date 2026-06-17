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

            // 2. Load product and resolve primary image URL via direct SQL projection.
            // Using a correlated subquery instead of Include(p => p.Images) avoids
            // navigation-collection loading issues with AsNoTracking.
            var productProjection = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    PrimaryImageUrl = _context.ProductImages
                        .Where(i => i.ProductId == p.Id)
                        .OrderBy(i => i.DisplayOrder)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (productProjection is null)
            {
                throw new ExternalServiceException("FalAi", "Product not found.");
            }

            // 3. Validate product has a primary image
            var productImageUrl = productProjection.PrimaryImageUrl;
            if (string.IsNullOrWhiteSpace(productImageUrl))
            {
                throw new ExternalServiceException("FalAi", "Product has no images for try-on.");
            }

            // 4. Build clothing prompt from product name
            var clothingPrompt = !string.IsNullOrWhiteSpace(productProjection.Name)
                ? productProjection.Name
                : "clothing item";

            _logger.LogInformation(
                "Starting fal.ai 3D try-on pipeline. Product: {ProductName}, Image: {ProductImageUrl}, BodyMesh: {BodyMeshUrl}, SourceImage: {SourceImageUrl}, FocalLength: {FocalLength}",
                clothingPrompt, productImageUrl, avatar.Avatar3dModelUrl, avatar.SourceImageUrl, avatar.AvatarFocalLength);

            // 5. Generate 3D model of the clothing item
            var objectGlb = await _falAiService.GenerateObject3dAsync(
                productImageUrl, clothingPrompt, cancellationToken);

            _logger.LogInformation(
                "Clothing 3D model generated. ObjectGlbUrl: {ObjectGlbUrl}",
                objectGlb.GlbUrl);

            // 6. Align body + clothing into one unified scene.
            //    SAM 3D Align needs the PERSON's original image (not the product image)
            //    as the reference for perspective-correct alignment.
            var alignImageUrl = avatar.SourceImageUrl ?? productImageUrl;
            var sceneGlbUrl = await _falAiService.AlignSceneAsync(
                imageUrl: alignImageUrl,
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
