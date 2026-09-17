using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin;

public class ImageInput
{
    public string? StorageKey { get; set; }
    public string? OriginalFileName { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? BlurDataUri { get; set; }
}

public class ArtworkCatalogAdditionForm : IValidatableObject
{
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

    [NonEmpty(ErrorMessage = "Add at least one image.")]
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
    }

    public ArtworkCatalogAddition ToCatalogAddition(ArtworkTypeId typeId)
    {
        ArgumentNullException.ThrowIfNull(Name);

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

        var dateCreated = PartialDate.FromParts(YearCreated, MonthCreated, DayCreated);

        return new ArtworkCatalogAddition(
            typeId,
            new ArtworkName(name),
            ArtworkSlug.FromName(name),
            Description,
            dateCreated,
            HeightCm.HasValue && WidthCm.HasValue
                ? new DimensionsCentimeters(new Dimensions(HeightCm.Value, WidthCm.Value, DepthCm))
                : null,
            Duration: null,
            Images: images,
            MainImageIndex: mainImageIndex,
            VocabularyTermIds: [.. VocabularyTermIds.Select(id => new VocabularyTermId(id))],
            SeriesIds: [.. SeriesIds.Select(id => new SeriesId(id))],
            Products: []
        );
    }

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
