using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using Microsoft.AspNetCore.Identity;

namespace ArtistShop.Web.Components.Pages.Platform.Register;

// form posts create this, and they need exactly one public constructor
public class ChoosePasswordForm : ServerValidatedForm
{
    // Identity's rules are checked as the account is made
    [Required]
    public string? Password { get; set; }

    [Compare(nameof(Password), ErrorMessage = "The passwords don't match.")]
    public string? ConfirmPassword { get; set; }

    public void AddPasswordErrors(IEnumerable<IdentityError> errors)
    {
        foreach (var error in errors)
        {
            AddServerError(nameof(Password), error.Description);
        }
    }
}
