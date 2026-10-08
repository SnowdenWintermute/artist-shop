using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain.Commerce;
using ArtistShop.Web.Imports;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.WorkImport;

// the choices the check form asks for, which the confirm form carries back in hidden fields
public abstract class WorkImportSettingsForm : ServerValidatedForm
{
    // null is only accepted for a type without height and width, where the page doesn't ask
    public LengthUnit? LengthUnit { get; set; }

    [Required(ErrorMessage = "Choose a product type.")]
    public int? ProductTypeId { get; set; }

    public bool IsOneOfAKind { get; set; } = true;

    // an attribute rather than IValidatableObject.Validate, which only runs once every attribute passes,
    // so its message would wait for the other fields to be fixed first
    [Required(ErrorMessage = "Choose a list separator.")]
    [RegularExpression(ImportListSeparator.Pattern, ErrorMessage = ImportListSeparator.Message)]
    public string? ListSeparator { get; set; } = ";";

    public void CopySettingsFrom(WorkImportSettingsForm other)
    {
        LengthUnit = other.LengthUnit;
        ProductTypeId = other.ProductTypeId;
        IsOneOfAKind = other.IsOneOfAKind;
        ListSeparator = other.ListSeparator;
    }

    public void AddLengthUnitRequiredError() =>
        AddServerError(nameof(LengthUnit), "Choose the unit the file's measurements are in.");

    // a type without height and width rejects length columns, so its unit is never used
    public WorkImportSettings ToSettings() =>
        new(
            LengthUnit ?? Imports.LengthUnit.Centimeters,
            new ProductTypeId(Unwrap.Value(ProductTypeId)),
            IsOneOfAKind,
            Unwrap.Value(ListSeparator)[0]
        );
}
