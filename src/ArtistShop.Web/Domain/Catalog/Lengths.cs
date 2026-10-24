namespace ArtistShop.Web.Domain.Catalog;

public enum LengthUnit : byte
{
    Centimeters = 1,
    Inches = 2,
}

// Lengths are saved in centimetres and entered in the unit the site's settings choose
public static class Lengths
{
    public const decimal CentimetersPerInch = 2.54m;

    // rounded down, so the largest length allowed still converts to centimetres the columns hold
    private static readonly decimal MaximumInches = Math.Round(
        ArtistShopLimits.MaximumDimensionCm / CentimetersPerInch,
        ArtistShopLimits.DimensionDecimalPlaces,
        MidpointRounding.ToZero
    );

    public static decimal Maximum(LengthUnit unit) =>
        unit is LengthUnit.Inches ? MaximumInches : ArtistShopLimits.MaximumDimensionCm;

    public static string Symbol(LengthUnit unit) => unit is LengthUnit.Inches ? "in" : "cm";

    public static string Name(LengthUnit unit) => unit is LengthUnit.Inches ? "inches" : "centimetres";

    // unrounded, for text that rounds it itself
    public static decimal InUnit(decimal centimeters, LengthUnit unit) =>
        unit is LengthUnit.Inches ? centimeters / CentimetersPerInch : centimeters;

    // For a field. Inches keep as many decimal places as the centimetres are saved with, so a length typed
    // in inches with up to that many comes back as typed. Dividing by 1 with that many zeros drops the
    // trailing ones, so the field shows 11.811 rather than 11.8110
    public static decimal ForEntry(decimal centimeters, LengthUnit unit) =>
        Math.Round(InUnit(centimeters, unit), ArtistShopLimits.DimensionDecimalPlaces) / 1.0000000000000000000000000000m;

    // A length shown in the field from saved and left unchanged keeps the saved centimetres. Converting it
    // back would move a length that was typed in centimetres: 30 cm shows as 11.811 in, which is 29.9999 cm
    public static decimal ToCentimeters(decimal length, LengthUnit unit, decimal? saved) =>
        saved is decimal savedCentimeters && ForEntry(savedCentimeters, unit) == length
            ? savedCentimeters
            : ToCentimeters(length, unit);

    // rounded as the columns keep it
    public static decimal ToCentimeters(decimal length, LengthUnit unit) =>
        Math.Round(unit is LengthUnit.Inches ? length * CentimetersPerInch : length, ArtistShopLimits.DimensionDecimalPlaces);
}
