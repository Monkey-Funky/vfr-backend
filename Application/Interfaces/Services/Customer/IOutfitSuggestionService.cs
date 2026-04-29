using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces.Services.Customer;

public interface IOutfitSuggestionService
{
    Task<List<OutfitSuggestionDto>> GenerateSuggestionsAsync(
        OutfitSuggestionRequest request,
        CancellationToken ct);
}

public sealed record OutfitSuggestionRequest(
    Guid CustomerId,
    string WeatherCondition,
    decimal TemperatureF,
    string Occasion,
    string? Mood,
    List<WardrobeProductInfo> WardrobeItems
);

public sealed record WardrobeProductInfo(
    Guid ProductId,
    string Name,
    string? Category,
    string? Material,
    string? Pattern,
    string? Occasion,
    string? PrimaryImageUrl,
    string[]? AvailableColors
);

public sealed record OutfitSuggestionDto(
    string Title,
    string Description,
    int MatchPercentage,
    string[] StyleTags,
    List<SuggestedItemDto> Items
);

public sealed record SuggestedItemDto(
    Guid ProductId,
    string Slot
);
