namespace Application.Features.Customer.Outfits.Commands.DeleteOutfit;

public sealed record DeleteOutfitCommand(Guid OutfitId) : IRequest;
