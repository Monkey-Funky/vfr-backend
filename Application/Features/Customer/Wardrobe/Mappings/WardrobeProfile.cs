using Domain.Entities.Customer;

namespace Application.Features.Customer.Wardrobe.Mappings;

public static class WardrobeMappings
{
    public static DTOs.WardrobeCollectionDto ToDto(this WardrobeCollection collection, int itemCount = 0, string? coverImageUrl = null)
    {
        return new DTOs.WardrobeCollectionDto(
            collection.Id,
            collection.Name,
            itemCount,
            coverImageUrl
        );
    }
}
