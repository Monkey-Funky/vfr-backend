using Application.Features.Customer.Outfits.DTOs;

namespace Application.Features.Customer.Outfits.Commands.UpdateOutfit;

public sealed record UpdateOutfitCommand(
    Guid OutfitId,
    string? Name,
    string? StyleCategory,
    List<UpdateOutfitItemDto> Items
) : IRequest;
