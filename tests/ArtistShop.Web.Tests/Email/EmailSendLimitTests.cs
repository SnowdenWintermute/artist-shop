using ArtistShop.Web.Email;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Tests.Email;

// how the limits are set up and keyed; the rate limiting itself is .NET's
public sealed class EmailSendLimitTests
{
    [Fact]
    public void ASecondEmailToAnAddressIsRefusedWhoeverAsks()
    {
        using var limit = new RateLimitedEmailSendLimit();

        Assert.True(limit.TryTake("someone@example.com", SignedOut("192.0.2.1")));
        Assert.False(limit.TryTake(" Someone@Example.com", SignedOut("192.0.2.2")));
        Assert.True(limit.TryTake("someone-else@example.com", SignedOut("192.0.2.1")));
    }

    [Fact]
    public void GmailAddressesThatDifferOnlyByDotsShareALimit()
    {
        using var limit = new RateLimitedEmailSendLimit();

        Assert.True(limit.TryTake("j.smith@gmail.com", SignedOut("192.0.2.1")));
        Assert.False(limit.TryTake("jsmith@googlemail.com", SignedOut("192.0.2.2")));
        // other providers can treat dots as part of the name
        Assert.True(limit.TryTake("j.smith@example.com", SignedOut("192.0.2.1")));
        Assert.True(limit.TryTake("jsmith@example.com", SignedOut("192.0.2.1")));
    }

    [Fact]
    public void AddressesThatDifferOnlyByAPlusTagShareALimit()
    {
        using var limit = new RateLimitedEmailSendLimit();

        Assert.True(limit.TryTake("someone@example.com", SignedOut("192.0.2.1")));
        Assert.False(limit.TryTake("someone+shop@example.com", SignedOut("192.0.2.2")));
        // with Gmail's dots too
        Assert.True(limit.TryTake("some.one+shop@gmail.com", SignedOut("192.0.2.1")));
        Assert.False(limit.TryTake("someone@gmail.com", SignedOut("192.0.2.2")));
    }

    // and the refused try doesn't count against the address
    [Fact]
    public void ARequesterIsRefusedAfterThirtyAddresses()
    {
        using var limit = new RateLimitedEmailSendLimit();

        for (var i = 0; i < 30; i++)
        {
            Assert.True(limit.TryTake($"{i}@example.com", SignedOut("192.0.2.1")));
        }

        Assert.False(limit.TryTake("30@example.com", SignedOut("192.0.2.1")));
        Assert.True(limit.TryTake("30@example.com", SignedOut("192.0.2.2")));
    }

    // so making more accounts doesn't earn more emails
    [Fact]
    public void SigningInDoesNotEscapeTheAddressLimit()
    {
        using var limit = new RateLimitedEmailSendLimit();

        for (var i = 0; i < 30; i++)
        {
            Assert.True(limit.TryTake($"{i}@example.com", new Requester("192.0.2.1", $"account-{i}")));
        }

        Assert.False(limit.TryTake("30@example.com", new Requester("192.0.2.1", "account-30")));
        Assert.False(limit.TryTake("30@example.com", SignedOut("192.0.2.1")));
    }

    [Fact]
    public void AnAccountIsRefusedAfterThirtyAddressesFromAnyAddress()
    {
        using var limit = new RateLimitedEmailSendLimit();

        for (var i = 0; i < 30; i++)
        {
            Assert.True(limit.TryTake($"{i}@example.com", new Requester($"192.0.2.{i}", "account-1")));
        }

        Assert.False(limit.TryTake("30@example.com", new Requester("192.0.2.30", "account-1")));
    }

    private static Requester SignedOut(string address) => new(address, null);
}
