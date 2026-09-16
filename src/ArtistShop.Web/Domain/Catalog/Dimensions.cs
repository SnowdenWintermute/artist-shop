namespace ArtistShop.Web.Domain.Catalog;

// height first, the order galleries list measurements in
public record Dimensions
{
    public decimal Height { get; }
    public decimal Width { get; }
    public decimal? Depth { get; }

    public Dimensions(decimal height, decimal width, decimal? depth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);

        if (depth is decimal knownDepth)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(knownDepth);
        }

        Height = height;
        Width = width;
        Depth = depth;
    }
}
