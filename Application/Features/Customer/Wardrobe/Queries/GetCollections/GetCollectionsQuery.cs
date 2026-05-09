using Application.Features.Customer.Wardrobe.DTOs;

namespace Application.Features.Customer.Wardrobe.Queries.GetCollections;

public sealed record GetCollectionsQuery : IRequest<List<WardrobeCollectionDto>>;
