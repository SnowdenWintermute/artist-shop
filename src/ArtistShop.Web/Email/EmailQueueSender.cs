namespace ArtistShop.Web.Email;

// Sends ChannelEmailQueue's emails one at a time, as they arrive. A failed send, such as SES refusing
// an address, is logged and not tried again
public sealed class EmailQueueSender(ChannelEmailQueue queue, Mailer mailer, ILogger<EmailQueueSender> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // not ended by stoppingToken: StopAsync closes the queue, and the loop ends once what's left
        // is sent
        await foreach (var message in queue.Reader.ReadAllAsync())
        {
            try
            {
                await mailer.SendAsync(message);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Couldn't send an email to {Recipient}", message.To);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        queue.Complete();
        await base.StopAsync(cancellationToken);
    }
}
