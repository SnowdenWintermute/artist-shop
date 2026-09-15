using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Domain.Commerce;

public record ShopItemId(int Value);

public record ShopItemName(string Value);

public record ShopItemImage(
    string RelativePath,
    string? OriginalFileName,
    int Width,
    int Height,
    string? BlurDataUri
);

public record ShopItemSlug(string Value)
{
    public static ShopItemSlug FromName(string name) => new(ArtistShopSlug.FromName(name));
}

public record ShopItemIdentifiers(ShopItemId Id, ShopItemSlug Slug);

public abstract class ShopItem(
    int id,
    ShopItemName name,
    ShopItemSlug slug,
    decimal? price,
    int stock,
    IEnumerable<ShopItemImage> images,
    IEnumerable<Series> series,
    IEnumerable<VocabularyTerm> vocabularyTerms
)
{
    public ShopItemId Id { get; } = new(id);
    public ShopItemName Name { get; set; } = name;
    public ShopItemSlug Slug { get; set; } = slug;

    public decimal? Price { get; set; } = price;
    public int Stock { get; set; } = stock;

    private readonly List<ShopItemImage> _images = [.. images];
    public IReadOnlyList<ShopItemImage> Images => _images;
    private readonly List<Series> _series = [.. series];
    public IReadOnlyList<Series> Series => _series;
    private readonly List<VocabularyTerm> _vocabularyTerms = [.. vocabularyTerms];
    public IReadOnlyList<VocabularyTerm> VocabularyTerms => _vocabularyTerms;

    // determine which thumbnail to show
    public int MainImageIndex { get; set; } = 0;
}
