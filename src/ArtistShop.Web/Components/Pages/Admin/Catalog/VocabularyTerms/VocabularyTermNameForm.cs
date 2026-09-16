using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.VocabularyTerms;

// form posts create this, and they need exactly one public constructor: with two, mapping
// fails with "does not have a constructor"
public class VocabularyTermNameForm : ServerValidatedForm
{
    public static VocabularyTermNameForm WithName(string name) => new() { Name = name };

    [Required]
    [StringLength(CatalogLimits.VocabularyTermNameMaximumLength)]
    public string? Name { get; set; }

    public void AddNameTakenError(string name)
    {
        AddServerError(nameof(Name), $"This vocabulary already has a term called \"{name}\".");
    }

    public VocabularyTermName ToVocabularyTermName() => new(Unwrap.Value(Name).Trim());
}
