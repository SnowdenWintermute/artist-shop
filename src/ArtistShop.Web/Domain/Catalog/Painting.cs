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
    IEnumerable<Series> series,
    IEnumerable<VocabularyTerm> vocabularyTerms
) : ShopItem(id, name, slug, price, stock, images, series, vocabularyTerms)
{
    public PartialDate? DatePainted { get; set; } = datePainted;

    public DimensionsCentimeters? Dimensions { get; private set; } = dimensions;
    public string? Description { get; private set; } = description;
}
