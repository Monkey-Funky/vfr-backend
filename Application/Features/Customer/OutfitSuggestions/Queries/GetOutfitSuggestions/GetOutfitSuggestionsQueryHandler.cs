using Application.Features.Customer.Outfits.DTOs;
using Application.Features.Customer.OutfitSuggestions.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Application.Interfaces.Services.Customer;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.OutfitSuggestions.Queries.GetOutfitSuggestions;

internal sealed class GetOutfitSuggestionsQueryHandler : IRequestHandler<GetOutfitSuggestionsQuery, List<OutfitSuggestionResultDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutfitSuggestionService _outfitSuggestionService;
    private readonly ILogger<GetOutfitSuggestionsQueryHandler> _logger;

    public GetOutfitSuggestionsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IOutfitSuggestionService outfitSuggestionService,
        ILogger<GetOutfitSuggestionsQueryHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _outfitSuggestionService = outfitSuggestionService;
        _logger = logger;
    }

    public async Task<List<OutfitSuggestionResultDto>> Handle(GetOutfitSuggestionsQuery request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedAccessException("Only authenticated customers can request outfit suggestions.");

        // 1. Fetch the user's Wardrobe (CustomerFavorites with Product details) via .AsNoTracking()
        var favorites = await _context.CustomerFavorites
            .AsNoTracking()
            .Where(f => f.CustomerId == customerId)
            .Select(f => f.ProductId)
            .ToListAsync(cancellationToken);

        var products = await _context.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .Where(p => favorites.Contains(p.Id) && p.Status == ProductStatus.Active)
            .ToListAsync(cancellationToken);

        // Fetch category names for pre-filter and DTO
        var categoryIds = products.Where(p => p.CategoryId.HasValue).Select(p => p.CategoryId!.Value).Distinct().ToList();
        var categoriesDict = await _context.Categories
            .AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        // W-4 Fix: Fetch inventory for stock status enrichment
        var productIds = products.Select(p => p.Id).ToList();
        var inventoryDict = await _context.InventoryRecords
            .AsNoTracking()
            .Where(ir => productIds.Contains(ir.ProductId))
            .ToDictionaryAsync(ir => ir.ProductId, cancellationToken);

        // PRE-FILTER STRATEGY: Filter the wardrobe items in memory based on the weather
        var filteredProducts = products.Where(p =>
        {
            if (request.TemperatureF > 70m)
            {
                var catName = p.CategoryId.HasValue && categoriesDict.TryGetValue(p.CategoryId.Value, out var name) ? name.ToLowerInvariant() : "";
                if (catName.Contains("jacket") || catName.Contains("coat") || catName.Contains("winter"))
                    return false;
            }
            else if (request.TemperatureF < 50m)
            {
                var catName = p.CategoryId.HasValue && categoriesDict.TryGetValue(p.CategoryId.Value, out var name) ? name.ToLowerInvariant() : "";
                if (catName.Contains("shorts") || catName.Contains("swimwear"))
                    return false;
            }
            return true;
        }).ToList();

        var wardrobeItems = filteredProducts.Select(p => new WardrobeProductInfo(
            p.Id,
            p.Name,
            p.CategoryId.HasValue && categoriesDict.TryGetValue(p.CategoryId.Value, out var catName) ? catName : null,
            p.Material,
            p.Pattern,
            p.Occasion,
            p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault(),
            p.AvailableColors
        )).ToList();

        var aiRequest = new OutfitSuggestionRequest(
            customerId,
            request.WeatherCondition,
            request.TemperatureF,
            request.Occasion,
            request.Mood,
            wardrobeItems
        );

        // W-6 Fix: Wrap AI call in try/catch for graceful degradation
        List<OutfitSuggestionDto> aiSuggestions;
        try
        {
            aiSuggestions = await _outfitSuggestionService.GenerateSuggestionsAsync(aiRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI outfit suggestion service failed for customer {CustomerId}. Returning empty suggestions.", customerId);
            return new List<OutfitSuggestionResultDto>();
        }

        // Enrich and return
        var productDict = products.ToDictionary(p => p.Id);

        var results = new List<OutfitSuggestionResultDto>();

        foreach (var suggestion in aiSuggestions)
        {
            var itemDtos = new List<OutfitItemDto>();
            int order = 0;
            
            foreach (var suggestedItem in suggestion.Items)
            {
                if (productDict.TryGetValue(suggestedItem.ProductId, out var product))
                {
                    // W-4 Fix: Compute stock status
                    string? stockStatus = null;
                    if (inventoryDict.TryGetValue(product.Id, out var inventory))
                    {
                        stockStatus = inventory.CurrentStock <= 0
                            ? "Out of Stock"
                            : inventory.CurrentStock <= inventory.LowStockThreshold
                                ? "Low Stock"
                                : "In Stock";
                    }

                    itemDtos.Add(new OutfitItemDto(
                        Guid.NewGuid(), // Ephemeral ID since this outfit isn't saved yet
                        product.Id,
                        suggestedItem.Slot,
                        order++,
                        product.Name,
                        product.Brand,
                        product.Price,
                        product.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault(),
                        product.AvailableColors,
                        stockStatus
                    ));
                }
            }

            results.Add(new OutfitSuggestionResultDto(
                suggestion.Title,
                suggestion.Description,
                suggestion.MatchPercentage,
                suggestion.StyleTags,
                itemDtos
            ));
        }

        return results;
    }
}
