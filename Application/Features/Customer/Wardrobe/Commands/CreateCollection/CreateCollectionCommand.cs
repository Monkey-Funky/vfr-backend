namespace Application.Features.Customer.Wardrobe.Commands.CreateCollection;

public sealed record CreateCollectionCommand(string Name) : IRequest<Guid>;
