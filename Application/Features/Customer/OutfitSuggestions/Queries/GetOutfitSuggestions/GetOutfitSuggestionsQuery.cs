using Application.Features.Customer.OutfitSuggestions.DTOs;

namespace Application.Features.Customer.OutfitSuggestions.Queries.GetOutfitSuggestions;

public sealed record GetOutfitSuggestionsQuery(
    string WeatherCondition,
    decimal TemperatureF,
    string Occasion,
    string? Mood
) : IRequest<List<OutfitSuggestionResultDto>>;