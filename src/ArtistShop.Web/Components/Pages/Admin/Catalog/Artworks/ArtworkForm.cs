using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Artworks;

public class ImageInput
{
    public string? StorageKey { get; set; }
    public string? OriginalFileName { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? BlurDataUri { get; set; }
}

// Behind both the add and the edit page. A [SupplyParameterFromForm] model is created by the
// binder, which needs exactly one public constructor, so an artwork being edited is read into the
// properties by FromArtwork rather than passed to a constructor.
public class ArtworkForm : IValidatableObject
{
    public static ArtworkForm FromArtwork(Artwork artwork) =>
        new()
        {
            Name = artwork.Name.Value,
            // the precision says which parts were given: the rest are the first of their period
            YearCreated = artwork.DateCreated?.Date.Year,
            MonthCreated = artwork.DateCreated is { Precision: >= DatePrecision.Month } toMonth
                ? toMonth.Date.Month
                : null,
            DayCreated = artwork.DateCreated is { Precision: DatePrecision.Day } toDay
                ? toDay.Date.Day
                : null,
            Description = artwork.Description,
            HeightCm = artwork.Dimensions?.Height,
            WidthCm = artwork.Dimensions?.Width,
            DepthCm = artwork.Dimensions?.Depth,
            Duration = artwork.Duration is TimeSpan duration
                ? ArtworkDuration.ToText(duration)
                : null,
            VocabularyTermIds = [.. artwork.VocabularyTerms.Select(term => term.Id.Value)],
            SeriesIds = [.. artwork.Series.Select(series => series.Id.Value)],
        };

    [Required]
    [StringLength(CatalogLimits.ArtworkNameMaximumLength)]
    public string? Name { get; set; }

    public int? YearCreated { get; set; }

    public int? MonthCreated { get; set; }

    public int? DayCreated { get; set; }

    public string? Description { get; set; }

    [Range(typeof(decimal), CatalogLimits.MinimumDimensionCm, CatalogLimits.MaximumDimensionCm)]
    public decimal? HeightCm { get; set; }

    [Range(typeof(decimal), CatalogLimits.MinimumDimensionCm, CatalogLimits.MaximumDimensionCm)]
    public decimal? WidthCm { get; set; }

    [Range(typeof(decimal), CatalogLimits.MinimumDimensionCm, CatalogLimits.MaximumDimensionCm)]
    public decimal? DepthCm { get; set; }

    // text rather than parts, because h:mm:ss is how a duration is written and read
    public string? Duration { get; set; }

    // only the add page posts these; the edit page leaves an artwork's images alone for now
    public List<ImageInput> Images { get; set; } = [];

    public string? PrimaryImageKey { get; set; }

    // form binding sets a list to null when no box was checked; "field" is the property's own backing field
    public List<int> VocabularyTermIds { get; set => field = value ?? []; } = [];

    public List<int> SeriesIds { get; set => field = value ?? []; } = [];

    // a field switched off in another tab isn't rendered any more, but its posted value would still be sent
    public void ClearFieldsOutside(IReadOnlyCollection<ArtworkField> fields)
    {
        if (!fields.Contains(ArtworkField.DateCreated))
        {
            YearCreated = null;
            MonthCreated = null;
            DayCreated = null;
        }

        if (!fields.Contains(ArtworkField.HeightAndWidth))
        {
            HeightCm = null;
            WidthCm = null;
        }

        if (!fields.Contains(ArtworkField.Depth))
        {
            DepthCm = null;
        }

        if (!fields.Contains(ArtworkField.Duration))
        {
            Duration = null;
        }
    }

    public ArtworkCatalogAddition ToCatalogAddition(ArtworkTypeId typeId)
    {
        var name = Unwrap.Value(Name);

        var images = Images
            .Select(image => new ArtworkImage(
                Unwrap.Value(image.StorageKey),
                image.OriginalFileName,
                image.Width,
                image.Height,
                image.BlurDataUri
            ))
            .ToList();

        var mainImageIndex = Math.Max(
            images.FindIndex(image => image.StorageKey == PrimaryImageKey),
            0
        );

        return new ArtworkCatalogAddition(
            typeId,
            new ArtworkName(name),
            ArtworkSlug.FromName(name),
            Description,
            ToDateCreated(),
            ToDimensions(),
            ToDuration(),
            Images: images,
            MainImageIndex: mainImageIndex,
            VocabularyTermIds: [.. VocabularyTermIds.Select(id => new VocabularyTermId(id))],
            SeriesIds: [.. SeriesIds.Select(id => new SeriesId(id))],
            NewSeriesNames: [],
            Products: []
        );
    }

    public ArtworkCatalogUpdate ToCatalogUpdate(ArtworkId id)
    {
        var name = Unwrap.Value(Name);

        return new ArtworkCatalogUpdate(
            id,
            new ArtworkName(name),
            // the procedure keeps the artwork's current slug when this one is the same name's
            ArtworkSlug.FromName(name),
            Description,
            ToDateCreated(),
            ToDimensions(),
            ToDuration(),
            VocabularyTermIds: [.. VocabularyTermIds.Select(id => new VocabularyTermId(id))],
            SeriesIds: [.. SeriesIds.Select(id => new SeriesId(id))]
        );
    }

    private PartialDate? ToDateCreated() => PartialDate.FromParts(YearCreated, MonthCreated, DayCreated);

    private DimensionsCentimeters? ToDimensions() =>
        HeightCm.HasValue && WidthCm.HasValue
            ? new DimensionsCentimeters(new Dimensions(HeightCm.Value, WidthCm.Value, DepthCm))
            : null;

    private TimeSpan? ToDuration() =>
        string.IsNullOrWhiteSpace(Duration) ? null : ArtworkDuration.TryParse(Duration);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (
            !PartialDate.TryFromParts(
                YearCreated,
                MonthCreated,
                DayCreated,
                out _,
                out var dateError
            )
        )
        {
            // translate "which part of the date" into "which property of this form"
            var memberName = dateError.Part switch
            {
                DatePart.Year => nameof(YearCreated),
                DatePart.Month => nameof(MonthCreated),
                DatePart.Day => nameof(DayCreated),
                _ => throw new UnreachableException(),
            };

            yield return new ValidationResult(dateError.Message, [memberName]);
        }

        var dimensionsPartiallyFilled = HeightCm.HasValue != WidthCm.HasValue;
        if (dimensionsPartiallyFilled)
        {
            yield return new ValidationResult(
                "Enter both height and width, or neither.",
                [nameof(HeightCm), nameof(WidthCm)]
            );
        }

        var depthWithoutHeightAndWidth = DepthCm.HasValue && !HeightCm.HasValue;
        if (depthWithoutHeightAndWidth)
        {
            yield return new ValidationResult(
                "Enter height and width to give a depth.",
                [nameof(DepthCm)]
            );
        }

        var durationUnreadable = !string.IsNullOrWhiteSpace(Duration) && ToDuration() is null;
        if (durationUnreadable)
        {
            yield return new ValidationResult(
                $"\"{Duration}\" isn't a duration. Use {ArtworkDuration.ExpectedFormat}.",
                [nameof(Duration)]
            );
        }

        var noDerivableSlug = Name is not null && ArtworkSlug.FromName(Name).Value.Length is 0;
        if (noDerivableSlug)
        {
            yield return new ValidationResult(
                "This title has no letters or numbers to build a web address from.",
                [nameof(Name)]
            );
        }

        var primaryNotAmongImages =
            PrimaryImageKey is not null
            && !Images.Any(image => image.StorageKey == PrimaryImageKey);

        if (primaryNotAmongImages)
        {
            yield return new ValidationResult(
                "The main image is not one of the uploaded images.",
                [nameof(PrimaryImageKey)]
            );
        }
    }
}
