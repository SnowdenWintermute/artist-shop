using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

// a page's name and address; form posts create this, so it keeps a single public constructor
public class CustomPageDetailsForm : ServerValidatedForm, IValidatableObject
{
    public const string NameTakenError = "Another page has this name.";

    public static string AddressTakenError(CustomPageAddress address) => $"Another page has the address /{address.Value}.";

    public static string OwnAddressError(CustomPageAddress address) => $"/{address.Value} is one of the website's own addresses.";

    [Required]
    [StringLength(ArtistShopLimits.CustomPageNameMaximumLength)]
    public string? Name { get; set; }

    [Required]
    public string? Address { get; set; }

    public static CustomPageDetailsForm From(CustomPage page) => new() { Name = page.Name, Address = page.Address.Value };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Address is not null && !new CustomPageAddress(Address.Trim()).IsWellFormed)
        {
            yield return new ValidationResult(
                "Use lower case letters, numbers and single dashes.",
                [nameof(Address)]
            );
        }
    }

    // false when the form says under a field why it wasn't saved
    public async Task<bool> SaveAsync(CustomPageId id, CustomPageRepository pages, OwnAddresses ownAddresses)
    {
        var name = Unwrap.Value(Name).Trim();
        var address = new CustomPageAddress(Unwrap.Value(Address).Trim());

        if (ownAddresses.Contains(address))
        {
            AddServerError(nameof(Address), OwnAddressError(address));
            return false;
        }

        try
        {
            await pages.UpdateAsync(id, name, address);
            return true;
        }
        catch (NameAlreadyInUseException)
        {
            AddServerError(nameof(Name), NameTakenError);
        }
        catch (AddressAlreadyInUseException)
        {
            AddServerError(nameof(Address), AddressTakenError(address));
        }

        return false;
    }
}
