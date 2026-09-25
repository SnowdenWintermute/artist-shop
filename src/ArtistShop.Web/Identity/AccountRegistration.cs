namespace ArtistShop.Web.Identity;

using System.Text;
using System.Text.Encodings.Web;
using ArtistShop.Web.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

// what AccountRegistration.RegisterAsync did
public abstract record RegistrationResult
{
    // only the ones below
    private RegistrationResult() { }

    // an email is on its way, whether the address had an account or not
    public sealed record EmailSent : RegistrationResult;

    // Identity's password rules refuse it
    public sealed record PasswordRefused(IReadOnlyList<IdentityError> Errors) : RegistrationResult;

    // Identity refuses the address itself, such as for a character it doesn't allow. No account
    // can have such an address, so saying so gives nothing away
    public sealed record EmailRefused(IReadOnlyList<IdentityError> Errors) : RegistrationResult;
}

// Makes an account, answering the same whether the email already has one or not, so the register
// page can't be used to find out who has an account. A new address gets a link to confirm it; one
// that has an account gets an email saying so, with links to sign in and reset the password
public sealed class AccountRegistration(UserManager<ApplicationUser> userManager, AccountEmails accountEmails)
{
    // hostRoot is this host's address, such as https://artshop.mikesilverman.net/, for the links
    public async Task<RegistrationResult> RegisterAsync(string email, string password, Uri hostRoot)
    {
        // before looking the email up, so a weak password gets the same answer either way
        var passwordErrors = await PasswordErrorsAsync(password);

        if (passwordErrors.Count > 0)
        {
            return new RegistrationResult.PasswordRefused(passwordErrors);
        }

        var existing = await userManager.FindByEmailAsync(email);

        if (existing is null)
        {
            var user = new ApplicationUser { UserName = email, Email = email };
            var creation = await userManager.CreateAsync(user, password);

            if (creation.Succeeded)
            {
                await SendConfirmationLinkAsync(user, email, hostRoot);
                return new RegistrationResult.EmailSent();
            }

            // someone registered the same email between the lookup and here
            if (!creation.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return new RegistrationResult.EmailRefused([.. creation.Errors]);
            }
        }
        else
        {
            // hashed and thrown away, so this answer takes about as long as making an account, whose
            // password is hashed as it's saved
            userManager.PasswordHasher.HashPassword(existing, password);
        }

        await accountEmails.SendAlreadyHaveAccountAsync(
            email,
            new Uri(hostRoot, "Account/Login").AbsoluteUri,
            new Uri(hostRoot, "Account/ForgotPassword").AbsoluteUri
        );

        return new RegistrationResult.EmailSent();
    }

    // the rules only look at the password, not the account, so a new account stands in for one
    private async Task<List<IdentityError>> PasswordErrorsAsync(string password)
    {
        var errors = new List<IdentityError>();

        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(userManager, new ApplicationUser(), password);
            errors.AddRange(result.Errors);
        }

        return errors;
    }

    // as ResendEmailConfirmation builds it, for Account/ConfirmEmail
    private async Task SendConfirmationLinkAsync(ApplicationUser user, string email, Uri hostRoot)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = QueryHelpers.AddQueryString(
            new Uri(hostRoot, "Account/ConfirmEmail").AbsoluteUri,
            new Dictionary<string, string?>
            {
                ["userId"] = user.Id,
                ["code"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)),
            }
        );

        await accountEmails.SendConfirmationLinkAsync(user, email, HtmlEncoder.Default.Encode(link));
    }
}
