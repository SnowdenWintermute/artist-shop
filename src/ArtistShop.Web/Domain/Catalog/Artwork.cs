using System.Globalization;
using System.Text.RegularExpressions;
using ArtistShop.Web.Domain.Commerce;

namespace ArtistShop.Web.Domain.Catalog;

public record ArtworkId(int Value);

public partial record ArtworkName(string Value)
{
    // macOS writes an accented letter as the plain letter followed by a combining mark, while
    // Windows and the database hold the single composed character. Normalize composes it, so the
    // same title matches whichever way the file name spells it
    public static ArtworkName FromFileName(string fileName) =>
        new(Path.GetFileNameWithoutExtension(fileName).Normalize());

    // No stored name can be longer than the name column allows, so one that is matches nothing
    public bool CanMatchAnArtwork => Value.Length <= ArtistShopLimits.ArtworkNameMaximumLength;

    // "Dawn (2)" read as another image of Dawn, as the image download names them: a space and a
    // whole number in brackets after the title. Null for any other name
    public ExtraImageName? AsExtraImage()
    {
        var match = ExtraImagePattern().Match(Value);

        return match.Success
            && int.TryParse(match.Groups[2].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            ? new ExtraImageName(new ArtworkName(match.Groups[1].Value), number)
            : null;
    }

    [GeneratedRegex(@"^(.+) \((-?\d+)\)$")]
    private static partial Regex ExtraImagePattern();
}

// Number orders the image among the artwork's others; any whole number, and gaps don't matter
public record ExtraImageName(ArtworkName Artwork, int Number);

public record ArtworkImage(
    string StorageKey,
    string? OriginalFileName,
    int Width,
    int Height,
    string? BlurDataUri
);

// what appending an image left on the artwork: the image given, or one it already had with the
// same bytes, in which case nothing was appended
public record ImageAppendResult(ArtworkImage Image, bool Appended);

public record ArtworkSlug(string Value)
{
    public static ArtworkSlug FromName(string name) => new(ArtistShopSlug.FromName(name));
}

public record ArtworkIdentifiers(ArtworkId Id, ArtworkSlug Slug);

// an artwork as the import tells same-titled artworks apart
public record ArtworkTitleAndSlug(string Title, string Slug);

public class Artwork(
    ArtworkId id,
    ArtworkType type,
    ArtworkName name,
    ArtworkSlug slug,
    string? description,
    PartialDate? dateCreated,
    DimensionsCentimeters? dimensions,
    TimeSpan? duration,
    IEnumerable<ArtworkImage> images,
    IEnumerable<Series> series,
    IEnumerable<VocabularyTerm> vocabularyTerms,
    IEnumerable<Product> products
)
{
    public ArtworkId Id { get; } = id;
    public ArtworkType Type { get; } = type;
    public ArtworkName Name { get; set; } = name;
    public ArtworkSlug Slug { get; set; } = slug;
    public string? Description { get; private set; } = description;
    public PartialDate? DateCreated { get; set; } = dateCreated;
    public DimensionsCentimeters? Dimensions { get; private set; } = dimensions;
    public TimeSpan? Duration { get; private set; } = duration;

    private readonly List<ArtworkImage> _images = [.. images];
    public IReadOnlyList<ArtworkImage> Images => _images;
    private readonly List<Series> _series = [.. series];
    public IReadOnlyList<Series> Series => _series;
    private readonly List<VocabularyTerm> _vocabularyTerms = [.. vocabularyTerms];
    public IReadOnlyList<VocabularyTerm> VocabularyTerms => _vocabularyTerms;
    private readonly List<Product> _products = [.. products];
    public IReadOnlyList<Product> Products => _products;

    // determine which thumbnail to show
    public int MainImageIndex { get; set; } = 0;
}
