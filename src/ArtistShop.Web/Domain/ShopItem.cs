namespace ArtistShop.Web.Domain;

public abstract class ShopItem(string id, string name, decimal price)
{
    public string Id { get; } = id;
    public string Name { get; set; } = name;
    public decimal Price { get; set; } = price;
    // stock = 1
    // views
}
