namespace ArtistShop.Web.Domain;

public abstract class ShopItem(string id, string name, decimal price, decimal stock)
{
    public string Id { get; } = id;
    public string Name { get; set; } = name;
    public decimal Price { get; set; } = price;
    public decimal Stock { get; set; } = stock;
}
