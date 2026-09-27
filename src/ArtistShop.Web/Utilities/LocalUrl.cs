namespace ArtistShop.Web.Utilities;

public static class LocalUrl
{
    // The url when it's a path on this host, otherwise null, so a crafted link can't send someone to
    // another website. "//evil.test" and "/\evil.test" are refused: browsers read both as another host
    public static string? OrNull(string? url) => url is "/" or ['/', not ('/' or '\\'), ..] ? url : null;
}
