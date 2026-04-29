using System;
using System.Collections.Generic;

namespace Application.Features.Customer.Outfits.DTOs;

public sealed record OutfitDetailDto(
    Guid Id,
    string Name,
    string? Style,
    DateTime CreatedAt,
    List<OutfitItemDto> Items
);
