namespace ArtistShop.Web.Domain.Catalog;

public record VocabularyId(int Value);

public record VocabularyName(string Value);

public record Vocabulary(VocabularyId Id, VocabularyName Name);

public record VocabularyWithShopItemTypes(
    VocabularyId Id,
    VocabularyName Name,
    IReadOnlySet<ShopItemTypeId> ShopItemTypeIds
);

public record VocabularyUsage(
    int VocabularyTermCount,
    IReadOnlyDictionary<ShopItemTypeId, int> ShopItemCountsByShopItemType
)
{
    // types with no items using this vocabulary have no entry
    public int ShopItemCountFor(ShopItemTypeId shopItemTypeId) =>
        ShopItemCountsByShopItemType.GetValueOrDefault(shopItemTypeId);

    // each item has exactly one type, so no item is counted twice
    public int TotalShopItemCount => ShopItemCountsByShopItemType.Values.Sum();
}
