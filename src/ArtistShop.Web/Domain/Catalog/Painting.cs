using ArtistShop.Web.Domain.Commerce;

namespace ArtistShop.Web.Domain.Catalog;

public class Painting(
    int id,
    string name,
    decimal price,
    int stock,
    DateOnly datePainted,
    IEnumerable<string> imageUrls,
    DimensionsCentimeters? dimensions,
    string? description,
    IEnumerable<SeriesId>? seriesIds,
    IEnumerable<Medium>? mediums,
    IEnumerable<Support>? supports
) : ShopItem(id, name, price, stock, imageUrls)
{
    public DateOnly DatePainted { get; set; } = datePainted;

    private readonly List<Medium> _mediums = mediums?.ToList() ?? [];
    public IReadOnlyList<Medium> Mediums => _mediums;
    private readonly List<Support> _supports = supports?.ToList() ?? [];
    public IReadOnlyList<Support> Supports => _supports;
    private readonly List<SeriesId> _seriesIds = seriesIds?.ToList() ?? [];
    public IReadOnlyList<SeriesId> SeriesIds => _seriesIds;

    public DimensionsCentimeters? Dimensions { get; private set; } = dimensions;
    public string? Description { get; private set; } = description;
}
