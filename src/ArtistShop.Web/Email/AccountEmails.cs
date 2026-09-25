namespace ArtistShop.Web.Email;

using System.Text.Encodings.Web;
using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Identity;

// The emails about an account. Identity's pages call the first three, handing over links they have
// already HTML-encoded
public sealed class AccountEmails(Mailer mailer) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        mailer.SendAsync(
            new EmailMessage(
                email,
                "Confirm your email",
                $"<p>Confirm your account by <a href=\"{confirmationLink}\">following this link</a>.</p>"
                    + "<p>If you didn't make an account, ignore this email.</p>"
            )
        );

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        mailer.SendAsync(
            new EmailMessage(
                email,
                "Reset your password",
                $"<p>Reset your password by <a href=\"{resetLink}\">following this link</a>.</p>"
                    + "<p>If you didn't ask to, ignore this email.</p>"
            )
        );

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        mailer.SendAsync(
            new EmailMessage(
                email,
                "Reset your password",
                $"<p>Reset your password with this code: {HtmlEncoder.Default.Encode(resetCode)}</p>"
            )
        );

    // Sent when someone registers with an email that already has an account, in place of a
    // confirmation link, so the register page answers the same either way and never says which
    // emails have accounts. The links aren't encoded yet
    public Task SendAlreadyHaveAccountAsync(string email, string signInLink, string resetLink) =>
        mailer.SendAsync(
            new EmailMessage(
                email,
                "You already have an account",
                "<p>Someone, perhaps you, tried to make an account with this email, which already has one.</p>"
                    + $"<p><a href=\"{HtmlEncoder.Default.Encode(signInLink)}\">Sign in</a>, or "
                    + $"<a href=\"{HtmlEncoder.Default.Encode(resetLink)}\">reset your password</a> if you've forgotten it.</p>"
                    + "<p>If it wasn't you, ignore this email: nothing has changed.</p>"
            )
        );
}
