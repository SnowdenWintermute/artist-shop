namespace ArtistShop.Web.Domain.Website;

// A label as the website prints it, capitalised and lowercased like a DisplayNoun
public record DisplayLabel(string Text, bool KeepsCase)
{
    public static DisplayLabel From(LabelChoice choice, string defaultText) => new(choice.Text ?? defaultText, choice.KeepsCase);

    public string Heading => WordCase.Heading(Text, KeepsCase);

    public string InSentence => WordCase.InSentence(Text, KeepsCase);
}
