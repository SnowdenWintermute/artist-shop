using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Collections;

// form posts create this, and they need exactly one public constructor: with two, mapping
// fails with "does not have a constructor"
public class CollectionNameForm : ServerValidatedForm, IValidatableObject
{
    public static CollectionNameForm WithName(string name) => new() { Name = name };

    [Required]
    [StringLength(ArtistShopLimits.CollectionNameMaximumLength)]
    public string? Name { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Name is not null && CollectionSlug.FromName(Name).Value.Length is 0)
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
            "This name, or one that only differs from it in punctuation, accents or capital letters, is taken."
        );
    }

    // the new collection, or null when the name is taken, which the form then says under the field
    public async Task<Collection?> AddAsync(CollectionRepository collectionRepository)
    {
        var name = ToCollectionName();
        var slug = ToCollectionSlug();

        try
        {
            return new Collection(await collectionRepository.AddAsync(name, slug), name, slug);
        }
        catch (NameAlreadyInUseException)
        {
            AddNameTakenError();
            return null;
        }
    }

    public CollectionName ToCollectionName() => new(Unwrap.Value(Name).Trim());

    public CollectionSlug ToCollectionSlug() => CollectionSlug.FromName(Unwrap.Value(Name));
}
