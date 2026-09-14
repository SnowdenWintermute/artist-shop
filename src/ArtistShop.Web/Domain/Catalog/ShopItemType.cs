namespace ArtistShop.Web.Domain.Catalog;

public record ShopItemTypeId(int Value);

public record ShopItemTypeName(string Value);

public record ShopItemType(ShopItemTypeId Id, ShopItemTypeName Name);
