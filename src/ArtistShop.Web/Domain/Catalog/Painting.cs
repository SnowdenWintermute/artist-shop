using ArtistShop.Web.Domain.Commerce;

namespace ArtistShop.Web.Domain.Catalog;

public class Painting(
    int id,
    ShopItemName name,
    ShopItemSlug slug,
    decimal? price,
    int stock,
    PartialDate? datePainted,
    IEnumerable<ShopItemImage> images,
    DimensionsCentimeters? dimensions,
    string? description,
    IEnumerable<PaintingSeries>? series,
    IEnumerable<VocabularyTerm> vocabularyTerms
) : ShopItem(id, name, slug, price, stock, images, vocabularyTerms)
{
    public PartialDate? DatePainted { get; set; } = datePainted;

    private readonly List<PaintingSeries> _series = series?.ToList() ?? [];
    public IReadOnlyList<PaintingSeries> Series => _series;

    public DimensionsCentimeters? Dimensions { get; private set; } = dimensions;
    public string? Description { get; private set; } = description;
}
