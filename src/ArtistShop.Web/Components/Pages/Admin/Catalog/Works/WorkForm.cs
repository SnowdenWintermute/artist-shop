using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Images;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Works;

public class ImageInput
{
    public string? StorageKey { get; set; }
    public string? OriginalFileName { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? BlurDataUri { get; set; }
}

// Behind both the add and the edit page. A [SupplyParameterFromForm] model is created by the
// binder, which needs exactly one public constructor, so a work being edited is read into the
// properties by FromWork rather than passed to a constructor.
public class WorkForm : IValidatableObject
{
    public static WorkForm FromWork(Work work, LengthUnit lengthUnit) =>
        new()
        {
            LengthUnit = lengthUnit,
            Name = work.Name.Value,
            YearCreated = work.DateCreated?.Date.Year,
            MonthCreated = work.DateCreated?.KnownMonth,
            DayCreated = work.DateCreated?.KnownDay,
            Description = work.Description,
            Height = LengthForEntry(work.Dimensions?.Height, lengthUnit),
            Width = LengthForEntry(work.Dimensions?.Width, lengthUnit),
            Depth = LengthForEntry(work.Dimensions?.Depth, lengthUnit),
            Duration = work.Duration is TimeSpan duration
                ? WorkDuration.ToText(duration)
                : null,
            Images =
            [
                .. work.Images.Select(image => new ImageInput
                {
                    StorageKey = image.StorageKey,
                    OriginalFileName = image.OriginalFileName,
                    Width = image.Width,
                    Height = image.Height,
                    BlurDataUri = image.BlurDataUri,
                }),
            ],
            // the first image is the main one unless another was picked, so only a real choice is
            // carried over: seeding it always would claim the artist chose an image they never touched
            PrimaryImageKey = work.MainImageIndex is 0
                ? null
                : work.Images[work.MainImageIndex].StorageKey,
            VocabularyTermIds = [.. work.VocabularyTerms.Select(term => term.Id.Value)],
            CollectionIds = [.. work.Collections.Select(collection => collection.Id.Value)],
        };

    // short messages, since the add-from-images table shows them under small cells
    [Required(ErrorMessage = "Enter a title.")]
    [StringLength(ArtistShopLimits.WorkNameMaximumLength, ErrorMessage = "Use at most {1} characters.")]
    public string? Name { get; set; }

    public int? YearCreated { get; set; }

    public int? MonthCreated { get; set; }

    public int? DayCreated { get; set; }

    public string? Description { get; set; }

    // What Height, Width and Depth are in. Posted with them, so a unit changed in another tab while this form
    // was open can't reread what was typed
    public LengthUnit LengthUnit { get; set; } = LengthUnit.Centimeters;

    [LengthRange]
    public decimal? Height { get; set; }

    [LengthRange]
    public decimal? Width { get; set; }

    [LengthRange]
    public decimal? Depth { get; set; }

    // text rather than parts, because h:mm:ss is how a duration is written and read
    public string? Duration { get; set; }

    // Form binding sets a list to null when the form posted nothing for it -- no box checked, or
    // every image removed -- and "field" is the property's own backing field.
    public List<ImageInput> Images { get; set => field = value ?? []; } = [];

    public string? PrimaryImageKey { get; set; }

    public List<int> VocabularyTermIds { get; set => field = value ?? []; } = [];

    public List<int> CollectionIds { get; set => field = value ?? []; } = [];

    // a field switched off in another tab isn't rendered any more, but its posted value would still be sent
    public void ClearFieldsOutside(IReadOnlyCollection<WorkField> fields)
    {
        if (!fields.Contains(WorkField.DateCreated))
        {
            YearCreated = null;
            MonthCreated = null;
            DayCreated = null;
        }

        if (!fields.Contains(WorkField.HeightAndWidth))
        {
            Height = null;
            Width = null;
        }

        if (!fields.Contains(WorkField.Depth))
        {
            Depth = null;
        }

        if (!fields.Contains(WorkField.Duration))
        {
            Duration = null;
        }
    }

    // a List, because it is passed to the images island as a parameter
    public List<WorkImage> ToWorkImages() =>
        [
            .. Images.Select(image => new WorkImage(
                Unwrap.Value(image.StorageKey),
                image.OriginalFileName,
                image.Width,
                image.Height,
                image.BlurDataUri
            )),
        ];

    // the images whose uploads the sweep removed while this form sat open
    public IReadOnlyList<ImageInput> ImagesMissingFrom(ImageStorage imageStorage) =>
        [.. Images.Where(image => !imageStorage.OriginalExists(image.StorageKey))];

    public static string MissingImageMessage(ImageInput image) =>
        $"{image.OriginalFileName ?? "An image"} is no longer on the server because the form was open too long. Remove it and upload it again.";

    // no choice means the first image: the list and cover queries read the starred row only,
    // so a work with images always stars exactly one
    private int MainImageIndex(List<WorkImage> images) =>
        Math.Max(images.FindIndex(image => image.StorageKey == PrimaryImageKey), 0);

    public WorkCatalogAddition ToCatalogAddition(WorkTypeId typeId)
    {
        var name = Unwrap.Value(Name);
        var images = ToWorkImages();

        return new WorkCatalogAddition(
            typeId,
            new WorkName(name),
            WorkSlug.FromName(name),
            Description,
            ToDateCreated(),
            ToDimensions(saved: null),
            ToDuration(),
            Images: images,
            MainImageIndex: MainImageIndex(images),
            VocabularyTermIds: [.. VocabularyTermIds.Select(id => new VocabularyTermId(id))],
            CollectionIds: [.. CollectionIds.Select(id => new CollectionId(id))],
            NewCollectionNames: [],
            Products: []
        );
    }

    // saved: the work's dimensions as the form was filled from them, which lengths left unchanged keep
    public WorkCatalogUpdate ToCatalogUpdate(WorkId id, DimensionsCentimeters? saved)
    {
        var images = ToWorkImages();

        return new WorkCatalogUpdate(ToDetailsUpdate(id, saved), images, MainImageIndex(images));
    }

    public WorkDetailsUpdate ToDetailsUpdate(WorkId id, DimensionsCentimeters? saved)
    {
        var name = Unwrap.Value(Name);

        return new WorkDetailsUpdate(
            id,
            new WorkName(name),
            // the procedure keeps the work's current slug when this one is the same name's
            WorkSlug.FromName(name),
            Description,
            ToDateCreated(),
            ToDimensions(saved),
            ToDuration(),
            VocabularyTermIds: [.. VocabularyTermIds.Select(id => new VocabularyTermId(id))],
            CollectionIds: [.. CollectionIds.Select(id => new CollectionId(id))]
        );
    }

    private PartialDate? ToDateCreated() => PartialDate.FromParts(YearCreated, MonthCreated, DayCreated);

    private static decimal? LengthForEntry(decimal? centimeters, LengthUnit unit) =>
        centimeters is decimal known ? Lengths.ForEntry(known, unit) : null;

    private DimensionsCentimeters? ToDimensions(DimensionsCentimeters? saved) =>
        Height is decimal height && Width is decimal width
            ? new DimensionsCentimeters(new Dimensions(
                Lengths.ToCentimeters(height, LengthUnit, saved?.Height),
                Lengths.ToCentimeters(width, LengthUnit, saved?.Width),
                Depth is decimal depth ? Lengths.ToCentimeters(depth, LengthUnit, saved?.Depth) : null
            ))
            : null;

    private TimeSpan? ToDuration() =>
        string.IsNullOrWhiteSpace(Duration) ? null : WorkDuration.TryParse(Duration);

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

        // under the empty one only, where the artist goes next. A height that isn't a number reads
        // as empty here too, so the width beside it isn't blamed for it
        if (Height.HasValue && !Width.HasValue)
        {
            yield return new ValidationResult("Enter a width too.", [nameof(Width)]);
        }

        if (Width.HasValue && !Height.HasValue)
        {
            yield return new ValidationResult("Enter a height too.", [nameof(Height)]);
        }

        var depthWithoutHeightAndWidth = Depth.HasValue && !Height.HasValue;
        if (depthWithoutHeightAndWidth)
        {
            yield return new ValidationResult(
                "Enter height and width to give a depth.",
                [nameof(Depth)]
            );
        }

        // a post a script made, or an edited page
        if (!Enum.IsDefined(LengthUnit))
        {
            yield return new ValidationResult("Unknown length unit.", [nameof(LengthUnit)]);
        }

        var durationUnreadable = !string.IsNullOrWhiteSpace(Duration) && ToDuration() is null;
        if (durationUnreadable)
        {
            yield return new ValidationResult(
                $"\"{Duration}\" isn't a duration. Use {WorkDuration.ExpectedFormat}.",
                [nameof(Duration)]
            );
        }

        var noDerivableSlug = Name is not null && WorkSlug.FromName(Name).Value.Length is 0;
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
