namespace Application.Features.Customer.Catalog.DTOs;

public sealed record ProductImageDto(
    Guid Id,
    string ImageUrl,
    int DisplayOrder
);
