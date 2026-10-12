namespace ArtistShop.Web.Domain.Website;

using ArtistShop.Web.Utilities;

// A noun as the website prints it: capitalised in a heading and lowercase inside a sentence, unless
// the artist keeps the capitals as typed, as a word like "NFTs" needs
public record DisplayNoun(string Singular, string Plural, bool KeepsCase)
{
    public static DisplayNoun From(NounChoice choice, string defaultSingular, string defaultPlural) =>
        new(choice.Singular ?? defaultSingular, choice.Plural ?? defaultPlural, choice.KeepsCase);

    public string SingularHeading => WordCase.Heading(Singular, KeepsCase);
    public string PluralHeading => WordCase.Heading(Plural, KeepsCase);
    public string SingularInSentence => WordCase.InSentence(Singular, KeepsCase);
    public string PluralInSentence => WordCase.InSentence(Plural, KeepsCase);

    // "1 work", "12 works"
    public string CountOf(int count) => CountText.Of(count, SingularInSentence, PluralInSentence);
}
