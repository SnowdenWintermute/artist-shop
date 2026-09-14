using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
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

public class PaintingCatalogAdditionForm : IValidatableObject
{
    [Required]
    [StringLength(CatalogLimits.ShopItemNameMaximumLength)]
    public string? Name { get; set; }

    [Range(typeof(decimal), CatalogLimits.MinimumPrice, CatalogLimits.MaximumPrice)]
    public decimal? Price { get; set; }

    public int? YearPainted { get; set; }

    public int? MonthPainted { get; set; }

    public int? DayPainted { get; set; }

    public string? Description { get; set; }

    [Range(typeof(decimal), CatalogLimits.MinimumDimensionCm, CatalogLimits.MaximumDimensionCm)]
    public decimal? WidthCm { get; set; }

    [Range(typeof(decimal), CatalogLimits.MinimumDimensionCm, CatalogLimits.MaximumDimensionCm)]
    public decimal? HeightCm { get; set; }

    [NonEmpty(ErrorMessage = "Add at least one image.")]
    public List<ImageInput> Images { get; set; } = [];

    public string? PrimaryImageKey { get; set; }

    public PaintingCatalogAddition ToCatalogAddition()
    {
        ArgumentNullException.ThrowIfNull(Name);

        var name = Unwrap.Value(Name);

        var images = Images
            .Select(image => new ShopItemImage(
                Unwrap.Value(image.StorageKey),
                image.OriginalFileName,
                image.Width,
                image.Height,
                image.BlurDataUri
            ))
            .ToList();

        var mainImageIndex = Math.Max(
            images.FindIndex(image => image.RelativePath == PrimaryImageKey),
            0
        );

        var datePainted = PartialDate.FromParts(YearPainted, MonthPainted, DayPainted);

        return new PaintingCatalogAddition(
            new ShopItemName(name),
            ShopItemSlug.FromName(name),
            Price,
            1,
            datePainted,
            Description,
            WidthCm.HasValue && HeightCm.HasValue
                ? new DimensionsCentimeters(new Dimensions(WidthCm.Value, HeightCm.Value))
                : null,
            Images: images,
            MainImageIndex: mainImageIndex,
            SeriesIds: [],
            VocabularyTermIds: []
        );
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (
            !PartialDate.TryFromParts(
                YearPainted,
                MonthPainted,
                DayPainted,
                out _,
                out var dateError
            )
        )
        {
            // translate "which part of the date" into "which property of this form"
            var memberName = dateError.Part switch
            {
                DatePart.Year => nameof(YearPainted),
                DatePart.Month => nameof(MonthPainted),
                DatePart.Day => nameof(DayPainted),
                _ => throw new UnreachableException(),
            };

            yield return new ValidationResult(dateError.Message, [memberName]);
        }

        var dimensionsPartiallyFilled = WidthCm.HasValue != HeightCm.HasValue;
        if (dimensionsPartiallyFilled)
        {
            yield return new ValidationResult(
                "Enter both width and height, or neither.",
                [nameof(WidthCm), nameof(HeightCm)]
            );
        }

        var noDerivableSlug = Name is not null && ShopItemSlug.FromName(Name).Value.Length is 0;
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
