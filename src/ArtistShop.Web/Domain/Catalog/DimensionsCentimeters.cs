namespace ArtistShop.Web.Domain.Catalog;

public record DimensionsCentimeters(Dimensions Value)
{
    public decimal Width => Value.Width;
    public decimal Height => Value.Height;
}
