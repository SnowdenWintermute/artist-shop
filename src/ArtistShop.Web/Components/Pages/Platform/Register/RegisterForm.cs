using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Utilities;
using Microsoft.AspNetCore.Identity;

namespace ArtistShop.Web.Components.Pages.Platform.Register;

// form posts create this, and they need exactly one public constructor
public class RegisterForm : ServerValidatedForm
{
    [Required]
    [EmailAddress]
    public string? Email { get; set; }

    public string TrimmedEmail => Unwrap.Value(Email).Trim();

    public void AddEmailErrors(IEnumerable<IdentityError> errors)
    {
        foreach (var error in errors)
        {
            AddServerError(nameof(Email), error.Description);
        }
    }
}
