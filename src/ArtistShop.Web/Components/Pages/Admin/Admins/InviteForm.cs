using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Admins;

// form posts create this, and they need exactly one public constructor
public class InviteForm : ServerValidatedForm, IValidatableObject
{
    [Required]
    public string? Email { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Email is not null && EmailAddress.Read(Email) is null)
        {
            yield return new ValidationResult("This isn't an email address.", [nameof(Email)]);
        }
    }

    public EmailAddress ToEmail() =>
        EmailAddress.Read(Unwrap.Value(Email)) ?? throw new InvalidOperationException("The email wasn't validated.");

    public void AddAlreadyMemberError() =>
        AddServerError(nameof(Email), "This account already helps run this website.");
}
