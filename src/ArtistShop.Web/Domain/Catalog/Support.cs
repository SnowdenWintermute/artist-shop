namespace ArtistShop.Web.Domain.Catalog;

public record SupportId(string Value);

public record SupportName(string Value);

public class Support(string id, string name)
{
    public SupportId Id { get; } = new(id);
    public SupportName Name { get; private set; } = new(name);
}
//         SUPPORT
// "canvas",
// "paper",
// "card stock",
// "vellum",
// "fabric",
// "stone",
// "wood",
// "metal",
// "fiber cloth",
// "wool felt",
// "tape",
// "canvas board",
// "board",
// "paper/matted",
// "",
