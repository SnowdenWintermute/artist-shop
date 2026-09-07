namespace ArtistShop.Web.Domain;

public class Painting(
    string id,
    string name,
    decimal price,
    DateOnly datePainted,
    string imageUrl,
    string thumbnailUrl,
    string? seriesId
) : ShopItem(id, name, price)
{
    public DateOnly DatePainted { get; set; } = datePainted;
    public string ImageUrl { get; set; } = imageUrl;
    public string ThumbnailUrl { get; set; } = thumbnailUrl;
    public string? SeriesId { get; set; } = seriesId;
    // surfaces
    // medium
}
