using System.Globalization;
using System.Text.RegularExpressions;
using ArtistShop.Web.Domain.Commerce;

namespace ArtistShop.Web.Domain.Catalog;

public record WorkId(int Value);

public partial record WorkName(string Value)
{
    // macOS writes an accented letter as the plain letter followed by a combining mark, while
    // Windows and the database hold the single composed character. Normalize composes it, so the
    // same title matches whichever way the file name spells it
    public static WorkName FromFileName(string fileName) =>
        new(Path.GetFileNameWithoutExtension(fileName).Normalize());

    // No stored name can be longer than the name column allows, so one that is matches nothing
    public bool CanMatchAnWork => Value.Length <= ArtistShopLimits.WorkNameMaximumLength;

    // "Dawn (2)" read as another image of Dawn, as the image download names them: a space and a
    // whole number in brackets after the title. Null for any other name
    public ExtraImageName? AsExtraImage()
    {
        var match = ExtraImagePattern().Match(Value);

        return match.Success
            && int.TryParse(match.Groups[2].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            ? new ExtraImageName(new WorkName(match.Groups[1].Value), number)
            : null;
    }

    [GeneratedRegex(@"^(.+) \((-?\d+)\)$")]
    private static partial Regex ExtraImagePattern();
}

// Number orders the image among the work's others; any whole number, and gaps don't matter
public record ExtraImageName(WorkName Work, int Number);

public record WorkImage(
    string StorageKey,
    string? OriginalFileName,
    int Width,
    int Height,
    string? BlurDataUri
);

// what appending an image left on the work: the image given, or one it already had with the
// same bytes, in which case nothing was appended
public record ImageAppendResult(WorkImage Image, bool Appended);

public record WorkSlug(string Value)
{
    public static WorkSlug FromName(string name) => new(ArtistShopSlug.FromName(name));
}

public record WorkIdentifiers(WorkId Id, WorkSlug Slug);

// a work as the import tells same-titled works apart
public record WorkTitleAndSlug(string Title, string Slug);

public class Work(
    WorkId id,
    WorkType type,
    WorkName name,
    WorkSlug slug,
    string? description,
    PartialDate? dateCreated,
    DimensionsCentimeters? dimensions,
    TimeSpan? duration,
    IEnumerable<WorkImage> images,
    IEnumerable<Collection> collections,
    IEnumerable<VocabularyTerm> vocabularyTerms,
    IEnumerable<Product> products
)
{
    public WorkId Id { get; } = id;
    public WorkType Type { get; } = type;
    public WorkName Name { get; set; } = name;
    public WorkSlug Slug { get; set; } = slug;
    public string? Description { get; private set; } = description;
    public PartialDate? DateCreated { get; set; } = dateCreated;
    public DimensionsCentimeters? Dimensions { get; private set; } = dimensions;
    public TimeSpan? Duration { get; private set; } = duration;

    private readonly List<WorkImage> _images = [.. images];
    public IReadOnlyList<WorkImage> Images => _images;
    private readonly List<Collection> _collections = [.. collections];
    public IReadOnlyList<Collection> Collections => _collections;
    private readonly List<VocabularyTerm> _vocabularyTerms = [.. vocabularyTerms];
    public IReadOnlyList<VocabularyTerm> VocabularyTerms => _vocabularyTerms;
    private readonly List<Product> _products = [.. products];
    public IReadOnlyList<Product> Products => _products;

    // determine which thumbnail to show
    public int MainImageIndex { get; set; } = 0;
}
