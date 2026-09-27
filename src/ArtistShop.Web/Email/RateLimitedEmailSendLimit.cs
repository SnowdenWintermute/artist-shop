namespace ArtistShop.Web.Email;

using System.Threading.RateLimiting;

// .NET's rate limiters, in memory: a restart forgets the counts, which only lets a few more emails
// through, and there's one app instance. An email is refused if any of the three limits is reached;
// the ones ahead of that limit still count it. The requester's comes first, so someone at their limit
// can't use up an address's by trying it again and again
public sealed class RateLimitedEmailSendLimit : EmailSendLimit, IDisposable
{
    private const int PerRecipientPerMinute = 1;
    // an hour rather than a day, so anyone who uses up an address's emails keeps its owner from a
    // reset link for an hour at most
    private const int PerRecipientPerHour = 5;
    // a household, a university or a phone carrier can share one address, so enough for a class
    // registering together
    private const int PerRequesterPerHour = 30;

    private readonly PartitionedRateLimiter<EmailRequest>[] _limits =
    [
        Limit(request => request.Requester, PerRequesterPerHour, TimeSpan.FromHours(1)),
        Limit(request => request.Recipient, PerRecipientPerMinute, TimeSpan.FromMinutes(1)),
        Limit(request => request.Recipient, PerRecipientPerHour, TimeSpan.FromHours(1)),
    ];

    private readonly PartitionedRateLimiter<EmailRequest> _chained;

    public RateLimitedEmailSendLimit() => _chained = PartitionedRateLimiter.CreateChained(_limits);

    public override bool TryTake(string recipient, string requester)
    {
        using var lease = _chained.AttemptAcquire(new EmailRequest(recipient.Trim().ToLowerInvariant(), requester));
        return lease.IsAcquired;
    }

    // the chained limiter leaves the ones it holds undisposed
    public void Dispose()
    {
        _chained.Dispose();

        foreach (var limit in _limits)
        {
            limit.Dispose();
        }
    }

    // A sliding window: each sixth of it, the emails counted in the oldest sixth are freed again, so
    // a burst at the end of one window and the start of the next still counts together
    private static PartitionedRateLimiter<EmailRequest> Limit(Func<EmailRequest, string> key, int permits, TimeSpan window) =>
        PartitionedRateLimiter.Create<EmailRequest, string>(request =>
            RateLimitPartition.GetSlidingWindowLimiter(
                key(request),
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = permits,
                    Window = window,
                    SegmentsPerWindow = 6,
                    // refused at once rather than held until a permit frees
                    QueueLimit = 0,
                }
            )
        );

    private sealed record EmailRequest(string Recipient, string Requester);
}
