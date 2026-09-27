using ArtistShop.Web.Email;

namespace ArtistShop.Web.Tests.App;

// TestApp's email queue: sends at once, so a test can read an email as soon as the request answers.
// The app's queue has tests of its own
public sealed class ImmediateEmailQueue(Mailer mailer) : EmailQueue
{
    public override Task EnqueueAsync(EmailMessage message) => mailer.SendAsync(message);
}
