namespace ArtistShop.Web.Domain.Catalog;

public record DimensionsCentimeters(Dimensions Value)
{
    public decimal Height => Value.Height;
    public decimal Width => Value.Width;
    public decimal? Depth => Value.Depth;
}
