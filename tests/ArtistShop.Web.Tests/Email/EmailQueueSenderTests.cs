using ArtistShop.Web.Email;
using ArtistShop.Web.Tests.App;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArtistShop.Web.Tests.Email;

// the app's queue, which TestApp swaps for one that sends at once
public sealed class EmailQueueSenderTests
{
    // stopping sends what's left, so the tests needn't wait for the background
    [Fact]
    public async Task QueuedEmailsAreSentInOrderAndWhatsLeftIsSentOnStopping()
    {
        var queue = new ChannelEmailQueue();
        var mailer = new CapturingMailer();
        var sender = new EmailQueueSender(queue, mailer, NullLogger<EmailQueueSender>.Instance);

        await sender.StartAsync(TestContext.Current.CancellationToken);
        await queue.EnqueueAsync(Message("someone@example.com", "first"));
        await queue.EnqueueAsync(Message("someone@example.com", "second"));
        await sender.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["first", "second"], mailer.SentTo("someone@example.com").Select(message => message.Subject));
    }

    [Fact]
    public async Task AFailedSendDoesNotStopTheOthers()
    {
        var queue = new ChannelEmailQueue();
        var mailer = new RefusingMailer("refused@example.com");
        var sender = new EmailQueueSender(queue, mailer, NullLogger<EmailQueueSender>.Instance);

        await sender.StartAsync(TestContext.Current.CancellationToken);
        await queue.EnqueueAsync(Message("refused@example.com", "first"));
        await queue.EnqueueAsync(Message("someone@example.com", "second"));
        await sender.StopAsync(TestContext.Current.CancellationToken);

        Assert.Single(mailer.Sent.SentTo("someone@example.com"));
    }

    [Fact]
    public async Task NothingCanBeQueuedOnceStopped()
    {
        var queue = new ChannelEmailQueue();
        var sender = new EmailQueueSender(queue, new CapturingMailer(), NullLogger<EmailQueueSender>.Instance);

        await sender.StartAsync(TestContext.Current.CancellationToken);
        await sender.StopAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.EnqueueAsync(Message("someone@example.com", "late")));
    }

    private static EmailMessage Message(string to, string subject) => new(to, subject, "<p>Hello</p>");

    // throws for one address, as SES does for one it won't send to
    private sealed class RefusingMailer(string refused) : Mailer
    {
        public CapturingMailer Sent { get; } = new();

        public override Task SendAsync(EmailMessage message) =>
            message.To == refused ? throw new InvalidOperationException($"Refused {refused}.") : Sent.SendAsync(message);
    }
}
