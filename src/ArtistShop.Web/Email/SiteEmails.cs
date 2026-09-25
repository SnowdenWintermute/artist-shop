namespace ArtistShop.Web.Email;

using System.Text.Encodings.Web;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Sites;

// The emails about being a member of a website. The links aren't encoded yet
public sealed class SiteEmails(Mailer mailer)
{
    public Task SendInviteAsync(EmailAddress to, string inviterEmail, HostName site, string mySitesLink) =>
        mailer.SendAsync(
            new EmailMessage(
                to.Value,
                $"You're invited to help run {site.Value}",
                $"<p>{HtmlEncoder.Default.Encode(inviterEmail)} invited you to be an admin of "
                    + $"{HtmlEncoder.Default.Encode(site.Value)}.</p>"
                    + $"<p>To accept, <a href=\"{HtmlEncoder.Default.Encode(mySitesLink)}\">sign in to My websites</a> "
                    + $"within {SiteInvite.DaysValid} days. If you don't have an account, make one with this email first.</p>"
                    + "<p>If you weren't expecting this, ignore this email.</p>"
            )
        );
}
