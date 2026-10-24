using System.Globalization;

namespace ArtistShop.Web.Domain.Catalog;

public record DimensionsCentimeters(Dimensions Value)
{
    public decimal Height => Value.Height;
    public decimal Width => Value.Width;
    public decimal? Depth => Value.Depth;

    public string Text(DimensionDisplay display) =>
        display switch
        {
            DimensionDisplay.Centimeters => TextIn(LengthUnit.Centimeters),
            DimensionDisplay.Inches => TextIn(LengthUnit.Inches),
            DimensionDisplay.InchesThenCentimeters => $"{TextIn(LengthUnit.Inches)} ({TextIn(LengthUnit.Centimeters)})",
            _ => throw new ArgumentOutOfRangeException(nameof(display), display, null),
        };

    // height first, the way a gallery label reads
    private string TextIn(LengthUnit unit) =>
        Depth is decimal depth
            ? $"{Number(Height, unit)} × {Number(Width, unit)} × {Number(depth, unit)} {Lengths.Symbol(unit)}"
            : $"{Number(Height, unit)} × {Number(Width, unit)} {Lengths.Symbol(unit)}";

    // "0.##" drops a trailing .00, so a whole number stays whole
    private static string Number(decimal centimeters, LengthUnit unit) =>
        Lengths.InUnit(centimeters, unit).ToString("0.##", CultureInfo.InvariantCulture);
}
