namespace ArtistShop.Web.Domain.Catalog;

public enum DimensionDisplay : byte
{
    Centimeters = 1,
    Inches = 2,
    // "12 × 16 in (30.5 × 40.6 cm)"
    InchesThenCentimeters = 3,
}

// site-wide, for every work type
public record WorkTypeSettings(DimensionDisplay DimensionDisplay)
{
    public static readonly WorkTypeSettings Default = new(DimensionDisplay.Centimeters);

    // the admin forms take lengths in the first unit shown
    public LengthUnit EnteredLengthUnit =>
        DimensionDisplay is DimensionDisplay.Centimeters ? LengthUnit.Centimeters : LengthUnit.Inches;
}
