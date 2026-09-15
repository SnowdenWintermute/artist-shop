using ArtistShop.Web.Domain.Commerce;

namespace ArtistShop.Web.Domain.Catalog;

public record SeriesId(int Value);

public record SeriesName(string Value);

public record SeriesSlug(string Value)
{
    public static SeriesSlug FromName(string name) => new(ArtistShopSlug.FromName(name));
}

public record Series(SeriesId Id, SeriesName Name, SeriesSlug Slug);

// Cover is null when no shop item in the series has an image
public record SeriesWithCover(
    SeriesId Id,
    SeriesName Name,
    SeriesSlug Slug,
    int ShopItemCount,
    ShopItemImage? Cover
);

public record SeriesShopItem(
    ShopItemId Id,
    ShopItemName Name,
    ShopItemTypeName ShopItemTypeName,
    bool IsCover,
    ShopItemImage? PrimaryImage
);

public record SeriesWithShopItems(
    SeriesId Id,
    SeriesName Name,
    SeriesSlug Slug,
    IReadOnlyList<SeriesShopItem> ShopItems
);
