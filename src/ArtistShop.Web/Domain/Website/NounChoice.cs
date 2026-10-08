namespace ArtistShop.Web.Domain.Website;

// What the artist chose to call one of the website's concepts. Null words mean the default
public record NounChoice(string? Singular, string? Plural, bool KeepsCase)
{
    public static readonly NounChoice Default = new(null, null, KeepsCase: false);

    // a word as it's saved: trimmed, and null when blank, which means the default
    public static string? Word(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
