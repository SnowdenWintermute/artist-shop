namespace ArtistShop.Web.Domain.Catalog;

public record SeriesId(int Value);

public record SeriesName(string Value);

public class PaintingSeries(int id, string name)
{
    public SeriesId Id { get; } = new(id);
    public SeriesName Name { get; private set; } = new(name);
}
