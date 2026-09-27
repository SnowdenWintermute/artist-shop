namespace ArtistShop.Web.Identity;

// A single-use code the platform makes once someone signed in there asks to sign in on a website,
// which that website's host trades for its own sign-in (SiteSignIns). Only the code's hash is kept.
// Nonce ties it to the browser that started: the website set it in a cookie before sending it here
public sealed class SiteSignInCode
{
    public required string CodeHash { get; init; }

    public required string UserId { get; init; }

    public required string SiteHost { get; init; }

    public required string Nonce { get; init; }

    // "remember me" on the platform's sign-in, carried to the website's
    public required bool IsPersistent { get; init; }

    // the platform sign-in that made the code, which the website's becomes part of
    public required string PlatformSessionId { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
