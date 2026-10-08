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
public record WorkImportSettings(
    LengthUnit LengthUnit,
    ProductTypeId ProductTypeId,
    bool IsOneOfAKind,
    char ListSeparator
);

// the header names a file uses; any other header must be a vocabulary's name
public static class WorkImportHeaders
{
    public const string Title = "title";
    public const string Slug = "slug";
    public const string Description = "description";
    public const string DateCreated = "dateCreated";
    public const string Height = "height";
    public const string Width = "width";
    public const string Depth = "depth";
    public const string Duration = "duration";
    public const string Collections = "collections";
    public const string Price = "price";
    public const string Sold = "sold";
    public const string EditionSize = "editionSize";
    public const string Stock = "stock";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [Title, Slug, Description, DateCreated, Height, Width, Depth, Duration, Collections, Price, Sold, EditionSize, Stock],
        ImportNames.Comparer
    );

    // A header starting with this always names a vocabulary, so one called "collections" can still have a
    // column. Without it, a header that is one of All means the import's own column
    public const string VocabularyPrefix = "vocabulary:";

    // the header a vocabulary's column needs: its name, with the prefix only when the name alone would
    // be read as something else
    public static string ForVocabulary(string vocabularyName) =>
        All.Contains(vocabularyName) || vocabularyName.StartsWith(VocabularyPrefix, StringComparison.OrdinalIgnoreCase)
            ? $"{VocabularyPrefix}{vocabularyName}"
            : vocabularyName;
}
