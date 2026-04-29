using Domain.Enums.Customer;

using Application.Features.Customer.Outfits.DTOs;

namespace Application.Features.Customer.Outfits.Commands.CreateOutfit;

public sealed record CreateOutfitCommand(
    string Name,
    string? StyleCategory,
    List<CreateOutfitItemDto> Items
) : IRequest<Guid>;
