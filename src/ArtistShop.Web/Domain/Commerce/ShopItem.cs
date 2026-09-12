using System.Globalization;
using System.Text;

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
    public static ShopItemSlug FromName(string name)
    {
        var normalized = name.Normalize(NormalizationForm.FormD);
        var slug = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) is UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                slug.Append(char.ToLowerInvariant(character));
            }
            else if (slug.Length > 0 && slug[^1] is not '-')
            {
                slug.Append('-');
            }
        }

        if (slug.Length > CatalogLimits.BaseSlugMaximumLength)
        {
            slug.Length = CatalogLimits.BaseSlugMaximumLength;
        }

        return new(slug.ToString().Trim('-'));
    }
}

public record ShopItemIdentifiers(ShopItemId Id, ShopItemSlug Slug);

public abstract class ShopItem(
    int id,
    ShopItemName name,
    ShopItemSlug slug,
    decimal price,
    int stock,
    IEnumerable<ShopItemImage> images
)
{
    public ShopItemId Id { get; } = new(id);
    public ShopItemName Name { get; set; } = name;
    public ShopItemSlug Slug { get; set; } = slug;

    public decimal Price { get; set; } = price;
    public int Stock { get; set; } = stock;

    private readonly List<ShopItemImage> _images = [.. images];
    public IReadOnlyList<ShopItemImage> Images => _images;

    // determine which thumbnail to show
    public int MainImageIndex { get; set; } = 0;
}
