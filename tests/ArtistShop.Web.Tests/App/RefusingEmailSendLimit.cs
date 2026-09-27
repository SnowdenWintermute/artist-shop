using System.Collections.Concurrent;
using ArtistShop.Web.Email;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Tests.App;

// TestApp's send limit: allows every email except to the addresses a test refuses, and keeps who asked
// for each. The real limits are .NET's rate limiters; these tests are about what the app does with
// each answer and who it says asked
public sealed class RefusingEmailSendLimit : EmailSendLimit
{
    private readonly ConcurrentDictionary<string, bool> _refused = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<(string Recipient, Requester Requester)> _asked = new();

    public void Refuse(string address) => _refused[address] = true;

    public override bool TryTake(string recipient, Requester requester)
    {
        _asked.Enqueue((recipient.Trim(), requester));
        return !_refused.ContainsKey(recipient.Trim());
    }

    // the tests each use addresses of their own, so reading by address keeps them apart
    public List<Requester> RequestersFor(string address) =>
        [.. _asked.Where(asked => string.Equals(asked.Recipient, address, StringComparison.OrdinalIgnoreCase)).Select(asked => asked.Requester)];
}
