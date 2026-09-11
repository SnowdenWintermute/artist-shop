using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin;

public class PaintingCatalogAdditionForm : IValidatableObject
{
    [Required]
    [StringLength(CatalogLimits.ShopItemNameMaximumLength)]
    public string? Name { get; set; }

    [Required]
    [Range(typeof(decimal), CatalogLimits.MinimumPrice, CatalogLimits.MaximumPrice)]
    public decimal? Price { get; set; }

    [Required]
    [Range(CatalogLimits.MinimumStock, int.MaxValue)]
    public int? Stock { get; set; }

    [Required]
    public DateOnly? DatePainted { get; set; }

    public string? Description { get; set; }

    [Range(typeof(decimal), CatalogLimits.MinimumDimensionCm, CatalogLimits.MaximumDimensionCm)]
    public decimal? WidthCm { get; set; }

    [Range(typeof(decimal), CatalogLimits.MinimumDimensionCm, CatalogLimits.MaximumDimensionCm)]
    public decimal? HeightCm { get; set; }

    public string? SpikeValue { get; set; }

    public PaintingCatalogAddition ToCatalogAddition()
    {
        ArgumentNullException.ThrowIfNull(Name);

        var name = Unwrap.Value(Name);

        return new PaintingCatalogAddition(
            new ShopItemName(name),
            ShopItemSlug.FromName(name),
            Unwrap.Value(Price),
            Unwrap.Value(Stock),
            Unwrap.Value(DatePainted),
            Description,
            WidthCm.HasValue && HeightCm.HasValue
                ? new DimensionsCentimeters(new Dimensions(WidthCm.Value, HeightCm.Value))
                : null,
            Images: [],
            MainImageIndex: 0,
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
    }
}
