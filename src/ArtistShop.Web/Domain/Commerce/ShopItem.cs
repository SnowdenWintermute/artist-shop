namespace ArtistShop.Web.Domain.Commerce;

public record ShopItemId(int Value);

public record ShopItemName(string Value);

public abstract class ShopItem(
    int id,
    string name,
    decimal price,
    int stock,
    IEnumerable<string> imageRelativeUrls
)
{
    public ShopItemId Id { get; } = new(id);
    public ShopItemName Name { get; set; } = new(name);
    public decimal Price { get; set; } = price;
    public int Stock { get; set; } = stock;

    private readonly List<string> _imageRelativeUrls = imageRelativeUrls?.ToList() ?? [];
    public IReadOnlyList<string> ImageRelativeUrls => _imageRelativeUrls;

    // determine which thumbnail to show
    public int MainImageIndex { get; set; } = 0;
}
