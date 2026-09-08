namespace ArtistShop.Web.Domain;

public class Painting(
    string id,
    string name,
    decimal price,
    decimal stock,
    DateOnly datePainted,
    string imageUrl,
    string thumbnailUrl,
    SeriesId? seriesId
) : ShopItem(id, name, price, stock)
{
    public DateOnly DatePainted { get; set; } = datePainted;
    public string ImageUrl { get; set; } = imageUrl;
    public string ThumbnailUrl { get; set; } = thumbnailUrl;
    public SeriesId? SeriesId { get; set; } = seriesId;
    // surfaces
    // medium
    // height
    // width
    // description?
}
