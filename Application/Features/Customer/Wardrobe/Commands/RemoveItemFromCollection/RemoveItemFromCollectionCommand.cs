namespace Application.Features.Customer.Wardrobe.Commands.RemoveItemFromCollection;

public sealed record RemoveItemFromCollectionCommand(Guid CollectionId, Guid ProductId) : IRequest;