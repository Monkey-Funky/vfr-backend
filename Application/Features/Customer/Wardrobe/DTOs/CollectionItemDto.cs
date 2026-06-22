namespace Application.Features.Customer.Wardrobe.DTOs;

/// <summary>
/// Represents a single item inside a wardrobe collection, returned by GET /items.
/// "Id" is the WardrobeCollectionItem row UUID — used by the client for DELETE /items/{id}.
/// "ProductId" is the product's own UUID.
/// </summary>
public sealed record CollectionItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string? ProductImageUrl,
    decimal? Price,
    DateTime AddedAt,
    Guid CollectionId
);
