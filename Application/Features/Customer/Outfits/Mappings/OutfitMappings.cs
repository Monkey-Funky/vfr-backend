using Application.Features.Customer.Outfits.DTOs;
using Domain.Entities.Customer;
using Domain.Entities.Retailer;

namespace Application.Features.Customer.Outfits.Mappings;

internal static class OutfitMappings
{
    public static OutfitSummaryDto ToOutfitSummaryDto(
        this CustomerOutfit outfit,
        Dictionary<Guid, string?> productsImages)
    {
        var slotPreviews = new Dictionary<string, string?>();
        // W-7 Fix: Only iterate non-deleted items
        var activeItems = outfit.Items.Where(i => !i.IsDeleted).OrderBy(i => i.DisplayOrder).ToList();
        
        foreach (var item in activeItems)
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
            activeItems.Count,
            slotPreviews
        );
    }

    public static OutfitDetailDto ToOutfitDetailDto(
        this CustomerOutfit outfit,
        Dictionary<Guid, Product> productsDict,
        Dictionary<Guid, InventoryRecord>? inventoryDict = null)
    {
        var itemDtos = new List<OutfitItemDto>();

        foreach (var item in outfit.Items.Where(i => !i.IsDeleted).OrderBy(i => i.DisplayOrder))
        {
            if (productsDict.TryGetValue(item.ProductId, out var product))
            {
                // Status check handles Retailer hiding products without breaking the outfit
                if (product.Status != Domain.Enums.Product.ProductStatus.Active)
                    continue;

                var primaryImage = product.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault();

                // W-4 Fix: Compute stock status from InventoryRecord
                string? stockStatus = null;
                if (inventoryDict != null && inventoryDict.TryGetValue(product.Id, out var inventory))
                {
                    stockStatus = inventory.CurrentStock <= 0
                        ? "Out of Stock"
                        : inventory.CurrentStock <= inventory.LowStockThreshold
                            ? "Low Stock"
                            : "In Stock";
                }

                itemDtos.Add(new OutfitItemDto(
                    item.Id,
                    item.ProductId,
                    item.SlotType.ToString(),
                    item.DisplayOrder,
                    product.Name,
                    product.Brand,
                    product.Price,
                    primaryImage,
                    product.AvailableColors,
                    stockStatus
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
