using Application.Features.Offers.DTOs;

namespace Application.Features.Offers.Mappings;

/// <summary>
/// Manual mapping extensions for Offer → OfferDto.
/// No AutoMapper, no Mapster — see 03-CodingStandards.md §7.
/// </summary>
public static class OfferMappings
{
    /// <summary>Maps a single <see cref="Offer"/> entity to its read DTO.</summary>
    public static OfferDto ToDto(this Offer offer)
        => new(
            Id: offer.Id,
            Title: offer.Title,
            Description: offer.Description,
            OfferType: offer.OfferType,
            ProductId: offer.ProductId,
            ProductName: offer.Product?.Name,    // populated only when Include(o => o.Product) used
            CategoryId: offer.CategoryId,
            CategoryName: offer.Category?.Name,   // populated only when Include(o => o.Category) used
            DiscountType: offer.DiscountType,
            DiscountValue: offer.DiscountValue,
            StartDate: offer.StartDate,
            EndDate: offer.EndDate,
            CoverImageUrl: offer.CoverImageUrl,
            Status: offer.Status,
            IsExpired: offer.IsExpired,
            IsActiveNow: offer.IsActive(DateTimeOffset.UtcNow),
            CreatedAt: offer.CreatedAt,
            UpdatedAt: offer.UpdatedAt
        );

    /// <summary>
    /// Projects a page of <see cref="Offer"/> entities into a
    /// <see cref="PagedResult{OfferDto}"/> using object-initialiser syntax.
    /// </summary>
    public static PagedResult<OfferDto> ToPagedDto(
        this IReadOnlyList<Offer> offers,
        int totalCount,
        int pageNumber,
        int pageSize)
        => new()
        {
            Items = offers.Select(o => o.ToDto()).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
}