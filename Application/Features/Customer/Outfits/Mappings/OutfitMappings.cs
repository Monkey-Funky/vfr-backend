using Application.Features.Customer.Outfits.DTOs;
using Domain.Entities.Customer;

namespace Application.Features.Customer.Outfits.Mappings;

internal static class OutfitMappings
{
    public static OutfitSummaryDto ToOutfitSummaryDto(
        this CustomerOutfit outfit,
        Dictionary<Guid, string?> productsImages)
    {
        var slotPreviews = new Dictionary<string, string?>();
        foreach (var item in outfit.Items.OrderBy(i => i.DisplayOrder))
        {
            var slotName = item.SlotType.ToString();
            
            if (!slotPreviews.ContainsKey(slotName))
            {
                productsImages.TryGetValue(item.ProductId, out var imgUrl);
                slotPreviews[slotName] = imgUrl;
            }
        }

        return new OutfitSummaryDto(
            outfit.Id,
            outfit.Name,
            outfit.StyleCategory,
            outfit.Items.Count,
            slotPreviews
        );
    }

    public static OutfitDetailDto ToOutfitDetailDto(
        this CustomerOutfit outfit,
        Dictionary<Guid, Product> productsDict)
    {
        var itemDtos = new List<OutfitItemDto>();

        foreach (var item in outfit.Items.OrderBy(i => i.DisplayOrder))
        {
            if (productsDict.TryGetValue(item.ProductId, out var product))
            {
                // Status check handles Retailer hiding products without breaking the outfit
                if (product.Status != Domain.Enums.Product.ProductStatus.Active)
                    continue;

                var primaryImage = product.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault();

                itemDtos.Add(new OutfitItemDto(
                    item.Id,
                    item.ProductId,
                    item.SlotType.ToString(),
                    item.DisplayOrder,
                    product.Name,
                    product.Brand,
                    product.Price,
                    primaryImage,
                    product.AvailableColors
                ));
            }
        }

        return new OutfitDetailDto(
            outfit.Id,
            outfit.Name,
            outfit.StyleCategory,
            outfit.CreatedAt,
            itemDtos
        );
    }
}
