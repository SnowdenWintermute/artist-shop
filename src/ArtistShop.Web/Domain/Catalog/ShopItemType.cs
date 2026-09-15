namespace ArtistShop.Web.Domain.Catalog;

public record ShopItemTypeId(int Value)
{
    // must match the 1 in Paintings.ShopItemTypeId and the ShopItemTypes seed row
    public static ShopItemTypeId Painting { get; } = new(1);
}

public record ShopItemTypeName(string Value);

public record ShopItemType(ShopItemTypeId Id, ShopItemTypeName Name);
