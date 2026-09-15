namespace ArtistShop.Web.Domain.Catalog;

public record VocabularyId(int Value);

public record VocabularyName(string Value);

public record Vocabulary(VocabularyId Id, VocabularyName Name);

// lists, not a set or dictionary: these are passed to interactive islands as JSON,
// and System.Text.Json can't read IReadOnlySet or dictionaries keyed by a record
public record VocabularyWithShopItemTypes(
    VocabularyId Id,
    VocabularyName Name,
    IReadOnlyList<ShopItemTypeId> ShopItemTypeIds
);

public record ShopItemTypeUsage(ShopItemTypeId ShopItemTypeId, int ShopItemCount);

public record VocabularyUsage(
    int VocabularyTermCount,
    IReadOnlyList<ShopItemTypeUsage> ShopItemTypeUsages
)
{
    // types with no items using this vocabulary have no entry
    public int ShopItemCountFor(ShopItemTypeId shopItemTypeId) =>
        ShopItemTypeUsages
            .FirstOrDefault(usage => usage.ShopItemTypeId == shopItemTypeId)
            ?.ShopItemCount ?? 0;

    // each item has exactly one type, so no item is counted twice
    public int TotalShopItemCount => ShopItemTypeUsages.Sum(usage => usage.ShopItemCount);
}
