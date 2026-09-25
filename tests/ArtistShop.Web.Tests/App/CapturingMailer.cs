using System.Collections.Concurrent;
using ArtistShop.Web.Email;

namespace ArtistShop.Web.Tests.App;

// TestApp's mailer: keeps each email rather than sending it, for the tests to read
public sealed class CapturingMailer : Mailer
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public override Task SendAsync(EmailMessage message)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    // the tests each use addresses of their own, so reading by address keeps them apart
    public List<EmailMessage> SentTo(string address) => [.. _sent.Where(message => message.To == address)];
}
