using Application.Features.Customer.Catalog.DTOs;
using Application.Features.Customer.Catalog.Queries.GetProductsByModelIds;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Application.Interfaces.Services.Customer;
using Domain.Enums.Product;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Catalog.Queries.GetSimilarProducts;

internal sealed class GetSimilarProductsQueryHandler
    : IRequestHandler<GetSimilarProductsQuery, List<ProductCardDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IComplementaryStyleService _aiStyleService;
    private readonly ISender _sender;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetSimilarProductsQueryHandler> _logger;

    public GetSimilarProductsQueryHandler(
        IApplicationDbContext context,
        IComplementaryStyleService aiStyleService,
        ISender sender,
        ICacheService cacheService,
        ILogger<GetSimilarProductsQueryHandler> logger)
    {
        _context = context;
        _aiStyleService = aiStyleService;
        _sender = sender;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<List<ProductCardDto>> Handle(
        GetSimilarProductsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Fetch the target product's AI ModelId
        var sourceProduct = await _context.Products
            .AsNoTracking()
            .Where(p => p.Id == request.ProductId && p.Status == ProductStatus.Active)
            .Select(p => new { p.Id, p.ModelId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        if (string.IsNullOrWhiteSpace(sourceProduct.ModelId))
        {
            _logger.LogWarning("Product {ProductId} has no ModelId. Cannot fetch similar items.", request.ProductId);
            return [];
        }

        // 2. Check Cache for the AI String IDs (Cache TTL: 1 Hour)
        string cacheKey = $"ai_similar_ids:{sourceProduct.ModelId}:{request.Limit}";
        //var cachedAiIds = await _cacheService.GetAsync<List<string>>(cacheKey, cancellationToken);
        List<string> cachedAiIds = null;

        List<string> aiMatches = cachedAiIds ?? [];

        if (cachedAiIds is null)
        {
            // 3. Cache Miss: Call the AI Service
            try
            {
                aiMatches = await _aiStyleService.GetSimilarItemsAsync(
                    sourceProduct.ModelId,
                    request.Limit,
                    cancellationToken);

                if (aiMatches.Count > 0)
                {
                    await _cacheService.SetAsync(
                        cacheKey,
                        aiMatches,
                        TimeSpan.FromHours(1),
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve similar items from AI for {ModelId}", sourceProduct.ModelId);
            }
        }

        if (aiMatches.Count == 0)
            return [];

        // 4. Delegate to your teammate's Query to hydrate the UI Data
        var hydratedProducts = await _sender.Send(
            new GetProductsByModelIdsQuery(aiMatches),
            cancellationToken);

        return hydratedProducts;
    }
}