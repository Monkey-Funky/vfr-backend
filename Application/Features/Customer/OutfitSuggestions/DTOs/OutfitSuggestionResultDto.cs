using Application.Features.Customer.Outfits.DTOs;

namespace Application.Features.Customer.OutfitSuggestions.DTOs;

public sealed record OutfitSuggestionResultDto(
    string Title,
    string Description,
    int MatchPercentage,
    string[] StyleTags,
    List<OutfitItemDto> Items
);
