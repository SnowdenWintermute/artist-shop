namespace ArtistShop.Web.Domain.Sites;

using System.Collections.Frozen;

// The name someone picks for their website at sign-up, which becomes the first part of its host:
// alice in alice.artshop.mikesilverman.net. Always lowercase, as a host is
public sealed record SiteName
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 30;

    // names the platform might want for itself, or that mail and DNS setups expect
    private static readonly FrozenSet<string> Reserved = FrozenSet.Create(
        "www", "admin", "api", "mail", "smtp", "imap", "pop", "ftp", "ns1", "ns2", "mx", "autoconfig",
        "autodiscover", "app", "static", "cdn", "assets", "media", "status", "docs", "help", "support",
        "billing", "login", "account", "signup", "operator", "platform", "dev", "staging", "test"
    );

    public string Value { get; }

    private SiteName(string value) => Value = value;

    public static SiteName? Read(string text) => ProblemWith(text) is null ? new SiteName(Normalize(text)) : null;

    // null for a name the rules allow; otherwise what's wrong with it, said to the person choosing it
    public static string? ProblemWith(string text)
    {
        var name = Normalize(text);

        if (name.Length is < MinimumLength or > MaximumLength)
        {
            return $"A name is {MinimumLength} to {MaximumLength} characters long.";
        }

        if (!name.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-'))
        {
            return "Use only the letters a to z, digits and hyphens.";
        }

        if (name.StartsWith('-') || name.EndsWith('-'))
        {
            return "A name can't start or end with a hyphen.";
        }

        // DNS keeps names like xn--… for spelling out names in other alphabets
        if (name.AsSpan(2).StartsWith("--"))
        {
            return "A name can't have hyphens as its third and fourth characters.";
        }

        if (Reserved.Contains(name))
        {
            return "This name is reserved.";
        }

        return null;
    }

    // the site's host: this name under the platform's
    public HostName HostUnder(HostName platformHost) =>
        HostName.Read($"{Value}.{platformHost.Value}")
        ?? throw new InvalidOperationException($"{Value}.{platformHost.Value} isn't a host name.");

    private static string Normalize(string text) => text.Trim().ToLowerInvariant();
}
