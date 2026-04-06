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
            CategoryId: offer.CategoryId,
            DiscountType: offer.DiscountType,
            DiscountValue: offer.DiscountValue,
            StartDate: offer.StartDate,
            EndDate: offer.EndDate,
            CoverImageUrl: offer.CoverImageUrl,
            Status: offer.Status,
            IsExpired: offer.IsExpired,
            CreatedAt: offer.CreatedAt,
            UpdatedAt: offer.UpdatedAt
        );

    /// <summary>
    /// Projects a page of <see cref="Offer"/> entities into a
    /// <see cref="PagedResult{OfferDto}"/> using object-initialiser syntax.
    ///
    /// NOTE: PagedResult&lt;T&gt; is a plain class with <c>init</c> setters
    /// (not a record with a positional constructor) — named-argument syntax
    /// is not valid here; use the property-initialiser form instead.
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