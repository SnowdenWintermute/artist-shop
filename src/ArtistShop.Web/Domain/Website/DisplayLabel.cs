namespace ArtistShop.Web.Domain.Website;

// A label as the website prints it, capitalised and lowercased like a DisplayNoun
public record DisplayLabel(string Text, bool KeepsCase)
{
    public static DisplayLabel From(LabelChoice choice, string defaultText) => new(choice.Text ?? defaultText, choice.KeepsCase);

    public string Heading => KeepsCase ? Text : char.ToUpperInvariant(Text[0]) + Text[1..];

    public string InSentence => KeepsCase ? Text : Text.ToLowerInvariant();
}
