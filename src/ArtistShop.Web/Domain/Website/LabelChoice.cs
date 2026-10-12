namespace ArtistShop.Web.Domain.Website;

// What the artist chose to call something with no plural, like a work's description. A null text
// means the default
public record LabelChoice(string? Text, bool KeepsCase)
{
    public static readonly LabelChoice Default = new(null, KeepsCase: false);
}
