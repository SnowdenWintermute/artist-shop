namespace ArtistShop.Web.Email;

using System.Threading.Channels;

// In memory. EmailQueueSender sends what's left when the app shuts down, so only a crash loses an
// email that was waiting. Unbounded, since emails are few
public sealed class ChannelEmailQueue : EmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>(
        new UnboundedChannelOptions { SingleReader = true }
    );

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    // closed only once EmailQueueSender stops, after the server has stopped taking requests
    public override Task EnqueueAsync(EmailMessage message) =>
        _channel.Writer.TryWrite(message)
            ? Task.CompletedTask
            : throw new InvalidOperationException($"The email queue is closed, so the email to {message.To} wasn't sent.");

    // Try: the host can stop EmailQueueSender more than once while shutting down, and Complete throws
    // on a queue that's already closed
    public void Complete() => _channel.Writer.TryComplete();
}
