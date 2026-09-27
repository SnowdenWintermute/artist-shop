namespace ArtistShop.Web.Email;

// Where the app hands over an email to be sent, so a page answers without waiting on SES and a failed
// send doesn't fail the page. ChannelEmailQueue in the app, which EmailQueueSender empties in the
// background; the tests' queue sends at once. A queue that keeps emails through a restart, such as
// Hangfire's, would be another of these
public abstract class EmailQueue
{
    public abstract Task EnqueueAsync(EmailMessage message);
}
