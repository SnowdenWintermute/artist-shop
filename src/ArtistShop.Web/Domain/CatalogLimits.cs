namespace ArtistShop.Web.Domain;

// updates to these must be mirrored in sql
public static class CatalogLimits
{
    public const int ShopItemImageFileNameMaximumLength = 260;
    public const int ShopItemNameMaximumLength = 200;
    public const int ShopItemSlugMaximumLength = 200;
    public const int BaseSlugMaximumLength = ShopItemSlugMaximumLength - 10;

    public const string MinimumPrice = "0";
    public const string MaximumPrice = "99999999.99";

    public const int MinimumStock = 0;

    public const string MinimumDimensionCm = "0.01";
    public const string MaximumDimensionCm = "9999.9999";

    public const int VocabularyNameMaximumLength = 100;
    public const int VocabularyTermNameMaximumLength = 100;
}
