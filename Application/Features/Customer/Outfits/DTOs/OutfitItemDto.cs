using System;

namespace Application.Features.Customer.Outfits.DTOs;

public sealed record OutfitItemDto(
    Guid Id,
    Guid ProductId,
    string Slot,
    int DisplayOrder,
    string ProductName,
    string? BrandName,
    decimal? Price,
    string? PrimaryImageUrl,
    string[]? AvailableColors
);
