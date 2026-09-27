namespace ArtistShop.Web.Identity;

using System.Text;
using System.Text.Encodings.Web;
using ArtistShop.Web.Components;
using ArtistShop.Web.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

// The one way to change a password: a link to Account/ResetPassword, emailed to the account's
// address, so changing it takes the inbox rather than a signed-in browser. Forgot password sends it
// to someone signed out, Manage's Password page to someone signed in
public sealed class PasswordResetLinks(
    UserManager<ApplicationUser> userManager,
    IEmailSender<ApplicationUser> emailSender,
    EmailSendLimit emailSendLimit
)
{
    // hostRoot is this host's address, such as https://artshop.mikesilverman.net/, for the link;
    // requester is EmailSendLimit.RequesterOf the request. False when the limit refused it and
    // nothing was sent
    public async Task<bool> SendAsync(ApplicationUser user, Uri hostRoot, string requester)
    {
        var email = await userManager.GetEmailAsync(user) ?? throw new InvalidOperationException($"Account {user.Id} has no email.");

        if (!emailSendLimit.TryTake(email, requester))
        {
            return false;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var link = QueryHelpers.AddQueryString(
            new Uri(hostRoot, PageUrls.ResetPassword).AbsoluteUri,
            "code",
            WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))
        );

        await emailSender.SendPasswordResetLinkAsync(user, email, HtmlEncoder.Default.Encode(link));
        return true;
    }
}
