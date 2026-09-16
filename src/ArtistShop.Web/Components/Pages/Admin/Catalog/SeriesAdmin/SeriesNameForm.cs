using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.SeriesAdmin;

// form posts create this, and they need exactly one public constructor: with two, mapping
// fails with "does not have a constructor"
public class SeriesNameForm : ServerValidatedForm, IValidatableObject
{
    public static SeriesNameForm WithName(string name) => new() { Name = name };

    [Required]
    [StringLength(CatalogLimits.SeriesNameMaximumLength)]
    public string? Name { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Name is not null && SeriesSlug.FromName(Name).Value.Length is 0)
        {
            yield return new ValidationResult(
                "This name has no letters or numbers to build a web address from.",
                [nameof(Name)]
            );
        }
    }

    public void AddNameTakenError()
    {
        AddServerError(
            nameof(Name),
            "Another series already has this name, or one that only differs in punctuation, accents or capital letters."
        );
    }

    public SeriesName ToSeriesName() => new(Unwrap.Value(Name).Trim());

    public SeriesSlug ToSeriesSlug() => SeriesSlug.FromName(Unwrap.Value(Name));
}
