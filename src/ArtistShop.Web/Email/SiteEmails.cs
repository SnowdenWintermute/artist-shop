namespace ArtistShop.Web.Email;

using System.Text.Encodings.Web;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Sites;

// The emails about being a member of a website. The links aren't encoded yet
public sealed class SiteEmails(Mailer mailer)
{
    public Task SendInviteAsync(
        EmailAddress to,
        string inviterEmail,
        HostName site,
        string mySitesLink
    ) =>
        mailer.SendAsync(
            new EmailMessage(
                to.Value,
                $"You're invited to help administer {site.Value}",
                $"<p>{HtmlEncoder.Default.Encode(inviterEmail)} invited you to be an admin of "
                    + $"{HtmlEncoder.Default.Encode(site.Value)}.</p>"
                    + $"<p>To accept, sign in at <a href=\"{HtmlEncoder.Default.Encode(mySitesLink)}\">"
                    + $"{HtmlEncoder.Default.Encode(mySitesLink)}</a> within {SiteInvite.DaysValid} days. If you don't "
                    + "have an account, make one there with this email first.</p>"
                    + "<p>If you weren't expecting this, ignore this email.</p>"
            )
        );

    public Task SendHandedOverToNewOwnerAsync(
        EmailAddress to,
        EmailAddress oldOwner,
        HostName site
    ) =>
        mailer.SendAsync(
            new EmailMessage(
                to.Value,
                $"You now own {site.Value}",
                $"<p>{HtmlEncoder.Default.Encode(oldOwner.Value)} transferred ownership of {HtmlEncoder.Default.Encode(site.Value)} "
                    + " to you.</p>"
            )
        );

    // so an owner hears of it even if someone else did it signed in as them
    public Task SendHandedOverToOldOwnerAsync(
        EmailAddress to,
        EmailAddress newOwner,
        HostName site
    ) =>
        mailer.SendAsync(
            new EmailMessage(
                to.Value,
                $"You handed {site.Value} over",
                $"<p>{HtmlEncoder.Default.Encode(newOwner.Value)} now owns {HtmlEncoder.Default.Encode(site.Value)}. "
                    + "You've been demoted to admin.</p>"
            )
        );

    public Task SendDeletedToOwnerAsync(
        EmailAddress to,
        HostName site,
        DateTimeOffset eraseAt,
        string mySitesLink
    ) =>
        mailer.SendAsync(
            new EmailMessage(
                to.Value,
                $"You deleted {site.Value}",
                $"<p>{HtmlEncoder.Default.Encode(site.Value)} is offline, and on {EmailDates.Text(eraseAt)} it will be erased "
                    + "along with all associated assets.</p>"
                    + $"<p>Until then you can keep it at <a href=\"{HtmlEncoder.Default.Encode(mySitesLink)}\">"
                    + $"{HtmlEncoder.Default.Encode(mySitesLink)}</a>.</p>"
            )
        );

    public Task SendDeletedToAdminAsync(
        EmailAddress to,
        EmailAddress owner,
        HostName site,
        DateTimeOffset eraseAt
    ) =>
        mailer.SendAsync(
            new EmailMessage(
                to.Value,
                $"{site.Value} was deleted",
                $"<p>{HtmlEncoder.Default.Encode(owner.Value)} deleted {HtmlEncoder.Default.Encode(site.Value)}. It's "
                    + $"offline, and on {EmailDates.Text(eraseAt)} it will be erased with all its artworks, posts and images, "
                    + "unless its owner keeps it.</p>"
            )
        );

    // the owner deleted their account, which takes their websites with it; nobody can keep them now
    public Task SendOwnerAccountDeletedToAdminAsync(EmailAddress to, EmailAddress owner, HostName site, DateTimeOffset eraseAt) =>
        mailer.SendAsync(
            new EmailMessage(
                to.Value,
                $"{site.Value} was deleted",
                $"<p>{HtmlEncoder.Default.Encode(owner.Value)} deleted their account, and with it "
                    + $"{HtmlEncoder.Default.Encode(site.Value)}. It's offline, and on {EmailDates.Text(eraseAt)} it will be "
                    + "erased with all its artworks, posts and images.</p>"
            )
        );
}
