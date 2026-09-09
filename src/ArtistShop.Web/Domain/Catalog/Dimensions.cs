namespace ArtistShop.Web.Domain.Catalog;

public record Dimensions
{
    public decimal Width { get; }
    public decimal Height { get; }

    public Dimensions(decimal width, decimal height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
    }
}
