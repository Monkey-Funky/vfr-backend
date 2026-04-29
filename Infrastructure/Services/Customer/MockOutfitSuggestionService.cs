using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces.Services.Customer;

namespace Infrastructure.Services.Customer;

internal sealed class MockOutfitSuggestionService : IOutfitSuggestionService
{
    public Task<List<OutfitSuggestionDto>> GenerateSuggestionsAsync(OutfitSuggestionRequest request, CancellationToken ct)
    {
        // MockOutfitSuggestionService: Return a "Casual Chic" outfit with a 95% MatchPercentage.
        
        // We will try to pick at least one Top and one Bottom from the requested wardrobe items,
        // just to make the mock return something valid from the customer's actual wardrobe.
        
        var top = request.WardrobeItems.FirstOrDefault(w => w.Category?.ToLowerInvariant().Contains("top") == true || w.Category?.ToLowerInvariant().Contains("shirt") == true)
               ?? request.WardrobeItems.FirstOrDefault(); // fallback

        var bottom = request.WardrobeItems.FirstOrDefault(w => w.Category?.ToLowerInvariant().Contains("bottom") == true || w.Category?.ToLowerInvariant().Contains("pant") == true)
                  ?? request.WardrobeItems.Skip(1).FirstOrDefault(); // fallback

        var items = new List<SuggestedItemDto>();
        
        if (top != null)
            items.Add(new SuggestedItemDto(top.ProductId, "Top"));
            
        if (bottom != null && bottom.ProductId != top?.ProductId)
            items.Add(new SuggestedItemDto(bottom.ProductId, "Bottom"));

        var result = new List<OutfitSuggestionDto>
        {
            new OutfitSuggestionDto(
                Title: "Casual Chic",
                Description: $"Perfect for a {request.WeatherCondition.ToLowerInvariant()} day.",
                MatchPercentage: 95,
                StyleTags: new[] { "Comfortable", "Versatile", "Timeless" },
                Items: items
            )
        };

        return Task.FromResult(result);
    }
}
