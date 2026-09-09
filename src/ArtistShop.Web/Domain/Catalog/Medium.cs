namespace ArtistShop.Web.Domain.Catalog;

public record MediumId(string Value);

public record MediumName(string Value);

public class Medium(string id, string name)
{
    public MediumId Id { get; } = new(id);
    public MediumName Name { get; private set; } = new(name);
}
//         "oil",
//         "pastel",
//         "oil pastel",
//         "watercolor",
//         "acrylic",
//         "pencil",
//         "graphite",
//         "colored pencil",
//         "charcoal",
//         "pencil and chalk",
//         "mono print",
//         "ink",
//         "pen and ink",
//         "reed and ink",
//         "crayon",
//         "mix media",
//         "clay",
//         "clay and wool",
//         "clear tape sculpture",
//         "acrylic collage",
//         "print",
//         "",
//
