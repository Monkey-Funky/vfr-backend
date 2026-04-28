using System;
using Domain.Enums.Customer;

namespace Application.Features.Customer.Outfits.DTOs;

public sealed record CreateOutfitItemDto(
    Guid ProductId, 
    SlotType SlotType, 
    int DisplayOrder
);
