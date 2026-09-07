namespace ArtistShop.Web.Domain;

public record SeriesId(string Value);

public record SeriesName(string Value);

public class PaintingSeries(string id, string name)
{
    public SeriesId Id { get; } = new(id);
    public SeriesName Name { get; private set; } = new(name);
}
