using Domain.Exceptions;

namespace Domain.Entities.Customer;

public sealed class WardrobeCollectionItem : BaseEntity
{
    public Guid CollectionId { get; private set; }
    public Guid FavoriteId { get; private set; }

    private WardrobeCollectionItem() { }

    public static WardrobeCollectionItem Create(Guid collectionId, Guid favoriteId)
    {
        if (collectionId == Guid.Empty)
            throw new BusinessRuleException("InvalidCollection", "CollectionId must not be empty.");

        if (favoriteId == Guid.Empty)
            throw new BusinessRuleException("InvalidFavorite", "FavoriteId must not be empty.");

        return new WardrobeCollectionItem
        {
            CollectionId = collectionId,
            FavoriteId = favoriteId
        };
    }

    public void SoftDelete() => MarkAsDeleted();
}