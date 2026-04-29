using Application.Features.Customer.Catalog.DTOs;
using Domain.Entities.Retailer;
using System;
using System.Linq;

namespace Application.Features.Customer.Catalog.Mappings;

public static class CatalogMappings
{
    public static ProductCardDto ToProductCardDto(this Product product, Offer? activeOffer, bool isFavorite)
    {
        decimal? discountedPrice = null;
        if (activeOffer != null && product.Price.HasValue)
        {
            discountedPrice = Math.Round(activeOffer.DiscountType == "Percentage" 
                ? product.Price.Value * (1 - activeOffer.DiscountValue / 100)
                : Math.Max(0, product.Price.Value - activeOffer.DiscountValue), 2);
        }

        string? primaryImage = product.Images?.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.ImageUrl;
        string? brandName = product.Brand;

        return new ProductCardDto(
            product.Id,
            product.Name,
            brandName,
            product.Price,
            discountedPrice,
            primaryImage,
            product.AvailableColors,
            isFavorite
        );
    }

    public static ProductComparisonDto ToProductComparisonDto(this Product product, Offer? activeOffer, string stockStatus)
    {
        decimal? discountedPrice = null;
        if (activeOffer != null && product.Price.HasValue)
        {
            discountedPrice = Math.Round(activeOffer.DiscountType == "Percentage" 
                ? product.Price.Value * (1 - activeOffer.DiscountValue / 100)
                : Math.Max(0, product.Price.Value - activeOffer.DiscountValue), 2);
        }

        string? primaryImage = product.Images?.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.ImageUrl;
        string? brandName = product.Brand;

        var attributes = new ProductAttributesDto(
            product.Material,
            product.Pattern,
            product.Lining,
            product.Length,
            product.Occasion,
            product.Neckline,
            product.Closure,
            product.Sleeves
        );

        return new ProductComparisonDto(
            product.Id,
            product.Name,
            brandName,
            product.Price,
            discountedPrice,
            primaryImage,
            null, // AverageRating
            stockStatus,
            attributes
        );
    }
}
