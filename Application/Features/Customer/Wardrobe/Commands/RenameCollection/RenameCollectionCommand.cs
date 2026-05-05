namespace Application.Features.Customer.Wardrobe.Commands.RenameCollection;

public sealed record RenameCollectionCommand(Guid CollectionId, string NewName) : IRequest;
