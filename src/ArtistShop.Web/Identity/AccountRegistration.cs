namespace ArtistShop.Web.Identity;

using System.Security.Cryptography;
using System.Text.Json;
using ArtistShop.Web.Components;
using ArtistShop.Web.Email;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

// what AccountRegistration.RegisterAsync did
public abstract record RegistrationResult
{
    // only the ones below
    private RegistrationResult() { }

    // an email is on its way, whether the address had an account or not, unless EmailSendLimit
    // refused it
    public sealed record EmailSent : RegistrationResult;

    // Identity refuses the address itself, such as for a character it doesn't allow. No account
    // can have such an address, so saying so gives nothing away
    public sealed record EmailRefused(IReadOnlyList<IdentityError> Errors) : RegistrationResult;
}

// what AccountRegistration.CreateAccountAsync did
public abstract record AccountCreationResult
{
    // only the ones below
    private AccountCreationResult() { }

    public sealed record Created(ApplicationUser User) : AccountCreationResult;

    // the link was used already, or the email got an account some other way since it was sent
    public sealed record AlreadyExists : AccountCreationResult;

    // Identity's password rules refuse it
    public sealed record PasswordRefused(IReadOnlyList<IdentityError> Errors) : AccountCreationResult;
}

// What the emailed link carries: the address it was sent to, and the page to go to once the account
// is made. ReturnUrl is only ever a path on the platform's host (LocalUrl.OrNull)
public sealed record RegistrationLink(string Email, string? ReturnUrl);

// Makes accounts in two steps, so no account exists until its email is proven. Registering asks only
// for an email and sends a link; the account is made, already confirmed, when that link's page is
// given a password. The link is signed and expires, so nothing is stored while it's on its way, and
// nobody can set the password of an address they can't read. Registering answers the same whether
// the email has an account or not, so the register page can't be used to find out who has one. An
// unconfirmed account, made before accounts waited for their email, counts as none: signing in and
// resetting its password both refuse it, and whoever chose its password never proved the address,
// so the link replaces that password and confirms it
public sealed class AccountRegistration(
    UserManager<ApplicationUser> userManager,
    AccountEmails accountEmails,
    EmailSendLimit emailSendLimit,
    IDataProtectionProvider dataProtection
)
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(24);

    // hostRoot is this host's address, such as https://artshop.mikesilverman.net/, for the links;
    // requester is EmailSendLimit.RequesterOf the request
    public async Task<RegistrationResult> RegisterAsync(string email, string? returnUrl, Uri hostRoot, string requester)
    {
        var emailErrors = await EmailErrorsAsync(email);

        if (emailErrors.Count > 0)
        {
            return new RegistrationResult.EmailRefused(emailErrors);
        }

        // over the limit, nothing is sent but the page answers the same, so it can't be used to learn
        // that someone else just asked for that address
        if (!emailSendLimit.TryTake(email, requester))
        {
            return new RegistrationResult.EmailSent();
        }

        if (await userManager.FindByEmailAsync(email) is not { EmailConfirmed: true })
        {
            await accountEmails.SendChoosePasswordLinkAsync(
                email,
                ChoosePasswordLink(new RegistrationLink(email, returnUrl), hostRoot),
                LinkLifetime
            );
        }
        else
        {
            await accountEmails.SendAlreadyHaveAccountAsync(
                email,
                new Uri(hostRoot, PageUrls.Login).AbsoluteUri,
                new Uri(hostRoot, PageUrls.ForgotPassword).AbsoluteUri
            );
        }

        return new RegistrationResult.EmailSent();
    }

    // null when it was altered, has expired, or was made with a key since retired
    public RegistrationLink? ReadLink(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RegistrationLink>(Protector().Unprotect(token));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public async Task<bool> HasConfirmedAccountAsync(RegistrationLink link) =>
        await userManager.FindByEmailAsync(link.Email) is { EmailConfirmed: true };

    public async Task<AccountCreationResult> CreateAccountAsync(RegistrationLink link, string password)
    {
        if (await userManager.FindByEmailAsync(link.Email) is { } existing)
        {
            return existing.EmailConfirmed
                ? new AccountCreationResult.AlreadyExists()
                : await ConfirmUnconfirmedAsync(existing, password);
        }

        // opening the link proved the address, so the account starts out confirmed
        var user = new ApplicationUser { UserName = link.Email, Email = link.Email, EmailConfirmed = true };
        var creation = await userManager.CreateAsync(user, password);

        if (creation.Succeeded)
        {
            return new AccountCreationResult.Created(user);
        }

        if (creation.Errors.Any(IsDuplicate))
        {
            return new AccountCreationResult.AlreadyExists();
        }

        return new AccountCreationResult.PasswordRefused([.. creation.Errors]);
    }

    // Replaces the password by a reset, which checks it against Identity's rules before changing
    // anything and gives the account a new security stamp
    private async Task<AccountCreationResult> ConfirmUnconfirmedAsync(ApplicationUser user, string password)
    {
        var reset = await userManager.ResetPasswordAsync(user, await userManager.GeneratePasswordResetTokenAsync(user), password);

        if (!reset.Succeeded)
        {
            return new AccountCreationResult.PasswordRefused([.. reset.Errors]);
        }

        user.EmailConfirmed = true;
        SeededAccounts.ThrowIfFailed(await userManager.UpdateAsync(user), "Confirming the email");

        return new AccountCreationResult.Created(user);
    }

    // Identity's rules for the account's user name and email, which are both the address. An address
    // that has an account passes here, so this answer doesn't say whether it has one
    private async Task<List<IdentityError>> EmailErrorsAsync(string email)
    {
        var errors = new List<IdentityError>();
        var user = new ApplicationUser { UserName = email, Email = email };

        foreach (var validator in userManager.UserValidators)
        {
            var result = await validator.ValidateAsync(userManager, user);
            errors.AddRange(result.Errors.Where(error => !IsDuplicate(error)));
        }

        return errors;
    }

    private static bool IsDuplicate(IdentityError error) => error.Code is "DuplicateEmail" or "DuplicateUserName";

    private string ChoosePasswordLink(RegistrationLink link, Uri hostRoot) =>
        QueryHelpers.AddQueryString(
            new Uri(hostRoot, PageUrls.ChoosePassword).AbsoluteUri,
            "token",
            Protector().Protect(JsonSerializer.Serialize(link), LinkLifetime)
        );

    // the purpose keeps these apart from anything else the app signs; time-limited ones carry their
    // expiry inside, and Unprotect refuses them after it
    private ITimeLimitedDataProtector Protector() =>
        dataProtection.CreateProtector("ArtistShop.RegistrationLink").ToTimeLimitedDataProtector();
}
