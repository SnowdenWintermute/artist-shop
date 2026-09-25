namespace ArtistShop.Web.Email;

using System.ComponentModel.DataAnnotations;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

// read from Email in configuration by ValidatedSettings. Dev sends to Mailpit (docker-compose.yml);
// production names its provider's server, and the login in its env file
public sealed record EmailSettings : IValidatableObject
{
    [Required]
    public string Host { get; init; } = "";

    [Range(1, 65535)]
    public int Port { get; init; }

    // None only for Mailpit, which has no TLS; a provider takes StartTls (587) or SslOnConnect (465)
    public SecureSocketOptions Security { get; init; } = SecureSocketOptions.StartTls;

    // both, or neither for a server that asks for no login, such as Mailpit
    public string? Username { get; init; }
    public string? Password { get; init; }

    [Required]
    [EmailAddress]
    public string FromAddress { get; init; } = "";

    [Required]
    public string FromName { get; init; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((Username is null) != (Password is null))
        {
            yield return new ValidationResult(
                "Username and Password are set together, or not at all.",
                [nameof(Username), nameof(Password)]
            );
        }
    }
}

public sealed class SmtpMailer(EmailSettings settings) : Mailer
{
    // a connection per email: an SmtpClient can't send two at once, and sign-up emails are few
    public override async Task SendAsync(EmailMessage message)
    {
        var mime = new MimeMessage
        {
            Subject = message.Subject,
            Body = new TextPart("html") { Text = message.HtmlBody },
        };
        mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, settings.Security);

        if (settings is { Username: { } username, Password: { } password })
        {
            await client.AuthenticateAsync(username, password);
        }

        await client.SendAsync(mime);
        await client.DisconnectAsync(quit: true);
    }
}
