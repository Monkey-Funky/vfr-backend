namespace Application.Features.Customer.Catalog.Queries.BrowseOffers;
public sealed record BrowseOffersQuery(Guid? RetailerId = null) : IRequest<PagedResult<OfferBrowseDto>>;

public sealed record OfferBrowseDto(
    Guid Id,
    string Title,
    string? Description,
    string OfferType,
    Guid? ProductId,
    Guid? CategoryId,
    string DiscountType,
    decimal DiscountValue,
    string CoverImageUrl,
    DateOnly StartDate,
    DateOnly? EndDate
);
