using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain.Platform;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Platform.SignUp;

// form posts create this, and they need exactly one public constructor
public class SignUpForm : ServerValidatedForm, IValidatableObject
{
    [Required]
    public string? Code { get; set; }

    // becomes the website's address
    [Required]
    public string? Name { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Code is not null && SignUpCode.Read(Code) is null)
        {
            yield return new ValidationResult("This isn't a sign-up code. Copy it as it was given to you.", [nameof(Code)]);
        }

        if (Name is not null && SiteName.ProblemWith(Name) is { } problem)
        {
            yield return new ValidationResult(problem, [nameof(Name)]);
        }
    }

    public SignUpCode ToCode() =>
        SignUpCode.Read(Unwrap.Value(Code)) ?? throw new InvalidOperationException("The code wasn't validated.");

    public SiteName ToSiteName() =>
        SiteName.Read(Unwrap.Value(Name)) ?? throw new InvalidOperationException("The name wasn't validated.");

    public void AddCodeNotUsableError() =>
        AddServerError(nameof(Code), "This code has expired or has already been used.");

    public void AddNameTakenError() => AddServerError(nameof(Name), "Another website has this name.");
}
