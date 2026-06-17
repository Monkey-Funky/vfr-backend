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
        var startTime = DateTime.UtcNow;

        _logger.LogInformation(
            "Processing Virtual Try-On for Customer {CustomerId} and Product {ProductId}",
            customerId, productId);

        // 1. Validate avatar has a 3D model URL (defence-in-depth; primary check is now
        //    in InitiateTryOnCommandHandler BEFORE session persistence — Issue 10 fix).
        //    Kept here so the service remains independently testable and self-contained.
        if (avatar?.Avatar3dModelUrl is null)
        {
            throw new BusinessRuleException("TryOn3DAvatarRequired",
                "Customer does not have a 3D avatar. Please create one first.");
        }

        // FIX (Issue 7): Require SourceImageUrl for the 3D align step.
        // The previous code silently fell back to the product image when SourceImageUrl
        // was null (avatar?.SourceImageUrl ?? productImageUrl). SAM 3D Align needs the
        // PERSON's photo for perspective-correct alignment — substituting the product
        // photo produces visually wrong results with no error surfaced to the customer.
        // We now fail loudly with a clear business rule error, consistent with how the
        // Overlay2D path handles the same missing-source-image condition.
        if (string.IsNullOrWhiteSpace(avatar.SourceImageUrl))
        {
            throw new BusinessRuleException("TryOn3DSourceImageMissing",
                "This avatar has no source image. Please re-create the avatar from a photo.");
        }

        // 2. Load product and resolve primary image URL via direct SQL projection.
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
            .FirstOrDefaultAsync(ct);

        if (productProjection is null)
        {
            throw new NotFoundException("Product", productId);
        }

        var productImageUrl = productProjection.PrimaryImageUrl;
        if (string.IsNullOrWhiteSpace(productImageUrl))
        {
            throw new BusinessRuleException("TryOn3DProductImageMissing",
                "This product has no image available for try-on.");
        }

        // 3. Build clothing prompt from product name
        var clothingPrompt = !string.IsNullOrWhiteSpace(productProjection.Name)
            ? productProjection.Name
            : "clothing item";

        _logger.LogInformation(
            "Starting fal.ai 3D try-on pipeline. Product: {ProductName}, Image: {ProductImageUrl}, BodyMesh: {BodyMeshUrl}, SourceImage: {SourceImageUrl}, FocalLength: {FocalLength}",
            clothingPrompt, productImageUrl, avatar.Avatar3dModelUrl, avatar.SourceImageUrl, avatar.AvatarFocalLength);

        // 4. The actual external fal.ai calls — these are the genuine network/external-
        //    service operations, so only THEY are protected by the shared "tryon"
        //    resilience pipeline (timeout + circuit breaker).
        var pipeline = _pipelineProvider.GetPipeline("tryon");

        return await pipeline.ExecuteAsync(async cancellationToken =>
        {
            // 4a. Generate 3D model of the clothing item
            var objectGlb = await _falAiService.GenerateObject3dAsync(
                productImageUrl, clothingPrompt, cancellationToken);

            _logger.LogInformation(
                "Clothing 3D model generated. ObjectGlbUrl: {ObjectGlbUrl}",
                objectGlb.GlbUrl);

            // 4b. Align body + clothing into one unified scene.
            //     SAM 3D Align needs the PERSON's original image as the reference
            //     for perspective-correct alignment. SourceImageUrl is guaranteed
            //     non-null here — validated in step 1 above.
            var sceneGlbUrl = await _falAiService.AlignSceneAsync(
                imageUrl: avatar.SourceImageUrl,   // person's photo — never substituted
                bodyMeshUrl: avatar.Avatar3dModelUrl,
                objectMeshUrl: objectGlb.GlbUrl,
                focalLength: avatar.AvatarFocalLength ?? 1000.0,
                cancellationToken);

            _logger.LogInformation(
                "3D scene alignment completed. SceneGlbUrl: {SceneGlbUrl}",
                sceneGlbUrl);

            // 4c. Calculate duration
            var durationSeconds = (int)(DateTime.UtcNow - startTime).TotalSeconds;

            return new TryOnResultDto(
                Status: SessionStatus.Completed,
                ResultImageUrl: sceneGlbUrl,
                RecommendedSize: null,
                ConfidenceScore: 0.98m,
                DurationSeconds: durationSeconds);
        }, ct);
    }
}
