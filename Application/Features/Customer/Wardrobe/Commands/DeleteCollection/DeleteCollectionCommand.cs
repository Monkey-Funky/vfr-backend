namespace Application.Features.Customer.Wardrobe.Commands.DeleteCollection;

public sealed record DeleteCollectionCommand(Guid CollectionId) : IRequest;
