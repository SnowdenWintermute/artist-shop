namespace ArtistShop.Web.Domain.Users;

public record ArtistShopUserId(string Value);

public class ArtistShopUser(string id)
{
    public ArtistShopUserId Id { get; } = new(id);

    // public Cart Cart { get; private set; }
}
