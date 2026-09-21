using System.Globalization;

namespace ArtistShop.Web.Domain.Catalog;

public record DimensionsCentimeters(Dimensions Value)
{
    public decimal Height => Value.Height;
    public decimal Width => Value.Width;
    public decimal? Depth => Value.Depth;

    // height first, the way a gallery label reads
    public string Text =>
        Depth is decimal depth
            ? $"{Number(Height)} × {Number(Width)} × {Number(depth)} cm"
            : $"{Number(Height)} × {Number(Width)} cm";

    // "0.##" drops a trailing .00, so a whole number of centimetres stays whole
    private static string Number(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
