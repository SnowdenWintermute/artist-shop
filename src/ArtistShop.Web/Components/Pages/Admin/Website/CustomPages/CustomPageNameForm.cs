using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

// a new page's name, its address made from it; form posts create this, so it keeps a single public constructor
public class CustomPageNameForm : ServerValidatedForm, IValidatableObject
{
    [Required]
    [StringLength(ArtistShopLimits.CustomPageNameMaximumLength)]
    public string? Name { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Name is not null && CustomPageAddress.FromName(Name).Value.Length is 0)
        {
            yield return new ValidationResult(
                "This name has no letters or numbers to build a web address from.",
                [nameof(Name)]
            );
        }
    }

    // the new page, or null when the form says under the field why it wasn't added
    public async Task<CustomPageId?> AddAsync(CustomPageRepository pages, OwnAddresses ownAddresses)
    {
        var name = Unwrap.Value(Name).Trim();
        var address = CustomPageAddress.FromName(name);

        if (ownAddresses.Contains(address))
        {
            AddServerError(nameof(Name), CustomPageDetailsForm.OwnAddressError(address));
            return null;
        }

        try
        {
            return await pages.AddAsync(name, address);
        }
        catch (NameAlreadyInUseException)
        {
            AddServerError(nameof(Name), CustomPageDetailsForm.NameTakenError);
        }
        catch (AddressAlreadyInUseException)
        {
            AddServerError(nameof(Name), CustomPageDetailsForm.AddressTakenError(address));
        }

        return null;
    }
}
