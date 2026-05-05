namespace Application.Features.Customer.Wardrobe.Commands.AddItemToCollection;

public sealed record AddItemToCollectionCommand(Guid CollectionId, Guid ProductId) : IRequest;
