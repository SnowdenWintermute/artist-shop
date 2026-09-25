namespace ArtistShop.Web.Email;

// an email to one address; the body is HTML, with anything a person typed already encoded
public sealed record EmailMessage(string To, string Subject, string HtmlBody);

// Sends email: SmtpMailer in the app, a fake that keeps what it was given in the tests
public abstract class Mailer
{
    public abstract Task SendAsync(EmailMessage message);
}
