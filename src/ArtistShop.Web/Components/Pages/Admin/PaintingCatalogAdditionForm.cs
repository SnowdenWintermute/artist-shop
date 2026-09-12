using System.ComponentModel.DataAnnotations;
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

    [Required]
    [Range(typeof(decimal), CatalogLimits.MinimumPrice, CatalogLimits.MaximumPrice)]
    public decimal? Price { get; set; }

    [Required]
    public DateOnly? DatePainted { get; set; }

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

        return new PaintingCatalogAddition(
            new ShopItemName(name),
            ShopItemSlug.FromName(name),
            Unwrap.Value(Price),
            1,
            Unwrap.Value(DatePainted),
            Description,
            WidthCm.HasValue && HeightCm.HasValue
                ? new DimensionsCentimeters(new Dimensions(WidthCm.Value, HeightCm.Value))
                : null,
            Images: images,
            MainImageIndex: mainImageIndex,
            MediumIds: [],
            SupportIds: [],
            SeriesIds: []
        );
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
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
