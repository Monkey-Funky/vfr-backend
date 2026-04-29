namespace Application.Features.Customer.Catalog.DTOs;

public sealed record ProductAttributesDto(
    string? Material,
    string? Pattern,
    string? Lining,
    string? Length,
    string? Occasion,
    string? Neckline,
    string? Closure,
    string? Sleeves
);
