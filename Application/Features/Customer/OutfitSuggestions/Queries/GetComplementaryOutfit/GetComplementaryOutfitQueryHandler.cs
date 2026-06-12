using Application.Features.Customer.Catalog.Queries.GetProductsByModelIds; 
using Application.Features.Customer.Catalog.DTOs; 
using Application.Interfaces.Persistence;
using Application.Interfaces.Services.Customer;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.OutfitSuggestions.Queries.GetComplementaryOutfits;

internal sealed class GetComplementaryOutfitsQueryHandler
    : IRequestHandler<GetComplementaryOutfitsQuery, List<ProductCardDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IComplementaryStyleService _styleService;
    private readonly ISender _sender;
    private readonly ILogger<GetComplementaryOutfitsQueryHandler> _logger;

    public GetComplementaryOutfitsQueryHandler(
        IApplicationDbContext context,
        IComplementaryStyleService styleService,
        ISender sender,
        ILogger<GetComplementaryOutfitsQueryHandler> logger)
    {
        _context = context;
        _styleService = styleService;
        _sender = sender;
        _logger = logger;
    }

    public async Task<List<ProductCardDto>> Handle(
        GetComplementaryOutfitsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Get the target product's AI ModelId
        var targetProduct = await _context.Products
            .AsNoTracking()
            .Where(p => p.Id == request.ProductId)
            .Select(p => new { p.Id, p.ModelId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        if (string.IsNullOrWhiteSpace(targetProduct.ModelId))
        {
            _logger.LogWarning("Product {ProductId} does not have a ModelId. Cannot fetch AI suggestions.", request.ProductId);
            return [];
        }

        // 2. Call the Python AI Model
        List<string> aiMatches;
        try
        {
            aiMatches = await _styleService.GetComplementaryItemsAsync(
                targetProduct.ModelId,
                request.TopK,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI style recommendation failed for product {ProductId}.", request.ProductId);
            return [];
        }

        if (aiMatches.Count == 0)
            return [];

        // 3. Delegate to your teammate's existing query!
        // This instantly converts the AI's string IDs into fully hydrated ProductCardDtos
        // with pricing, discounts, and favorite statuses.
        var hydratedProducts = await _sender.Send(
            new GetProductsByModelIdsQuery(aiMatches),
            cancellationToken);

        return hydratedProducts;
    }
}
