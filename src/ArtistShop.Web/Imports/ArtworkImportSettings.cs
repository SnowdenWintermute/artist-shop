using ArtistShop.Web.Domain.Commerce;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Imports;

public enum LengthUnit : byte
{
    Centimeters = 1,
    Inches = 2,
}

// IsOneOfAKind: every product is edition size 1 and a "sold" column sets its stock; otherwise the
// file gives "editionSize" and "stock" itself
public record ArtworkImportSettings(
    LengthUnit LengthUnit,
    ProductTypeId ProductTypeId,
    bool IsOneOfAKind,
    char ListSeparator
);

public static class ArtworkImportLimits
{
    // the review posts the text back in a form field, and ASP.NET Core refuses a field over 4 MB
    public const int FileMaximumBytes = 1 * Units.BytesPerMebibyte;
}

// the header names a file uses; any other header must be a vocabulary's name
public static class ArtworkImportHeaders
{
    public const string Title = "title";
    public const string Description = "description";
    public const string DateCreated = "dateCreated";
    public const string Height = "height";
    public const string Width = "width";
    public const string Depth = "depth";
    public const string Duration = "duration";
    public const string Series = "series";
    public const string Price = "price";
    public const string Sold = "sold";
    public const string EditionSize = "editionSize";
    public const string Stock = "stock";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [Title, Description, DateCreated, Height, Width, Depth, Duration, Series, Price, Sold, EditionSize, Stock],
        ArtworkImportNames.Comparer
    );
}

public static class ArtworkImportNames
{
    // close to the database's case-insensitive, accent-sensitive comparison of names
    public static readonly StringComparer Comparer = StringComparer.InvariantCultureIgnoreCase;
}
