namespace ArtistShop.Web.Utilities;

public static class ClassNames
{
    // a component's own classes plus whatever its caller adds, skipping the parts left empty
    public static string Join(params string?[] parts) =>
        string.Join(' ', parts.Where(part => !string.IsNullOrWhiteSpace(part)));
}
