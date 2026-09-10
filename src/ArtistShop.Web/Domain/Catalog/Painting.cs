using ArtistShop.Web.Domain.Commerce;

namespace ArtistShop.Web.Domain.Catalog;

public class Painting(
    int id,
    ShopItemName name,
    ShopItemSlug slug,
    decimal price,
    int stock,
    DateOnly datePainted,
    IEnumerable<string> imageRelativeUrls,
    DimensionsCentimeters? dimensions,
    string? description,
    IEnumerable<Medium>? mediums,
    IEnumerable<Support>? supports,
    IEnumerable<PaintingSeries>? series
) : ShopItem(id, name, slug, price, stock, imageRelativeUrls)
{
    public DateOnly DatePainted { get; set; } = datePainted;

    private readonly List<Medium> _mediums = mediums?.ToList() ?? [];
    public IReadOnlyList<Medium> Mediums => _mediums;
    private readonly List<Support> _supports = supports?.ToList() ?? [];
    public IReadOnlyList<Support> Supports => _supports;
    private readonly List<PaintingSeries> _series = series?.ToList() ?? [];
    public IReadOnlyList<PaintingSeries> Series => _series;

    public DimensionsCentimeters? Dimensions { get; private set; } = dimensions;
    public string? Description { get; private set; } = description;
}
