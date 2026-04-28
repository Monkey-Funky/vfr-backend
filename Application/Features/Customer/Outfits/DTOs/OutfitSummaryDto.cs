using System;
using System.Collections.Generic;

namespace Application.Features.Customer.Outfits.DTOs;

public sealed record OutfitSummaryDto(
    Guid Id,
    string Name,
    string? Style,
    int ItemCount,
    Dictionary<string, string?> SlotPreviews
);
