using System.Collections.Concurrent;
using ArtistShop.Web.Email;

namespace ArtistShop.Web.Tests.App;

// TestApp's send limit: allows every email except to the addresses a test refuses. The real limits
// are .NET's rate limiters; these tests are about what the app does with each answer
public sealed class RefusingEmailSendLimit : EmailSendLimit
{
    private readonly ConcurrentDictionary<string, bool> _refused = new(StringComparer.OrdinalIgnoreCase);

    public void Refuse(string address) => _refused[address] = true;

    public override bool TryTake(string recipient, string requester) => !_refused.ContainsKey(recipient.Trim());
}
