namespace ArtistShop.Web.Domain.Website;

using ArtistShop.Web.Utilities;

// A noun as the website prints it: capitalised in a heading and lowercase inside a sentence, unless
// the artist keeps the capitals as typed, as a word like "NFTs" needs
public record DisplayNoun(string Singular, string Plural, bool KeepsCase)
{
    public static DisplayNoun From(NounChoice choice, string defaultSingular, string defaultPlural) =>
        new(choice.Singular ?? defaultSingular, choice.Plural ?? defaultPlural, choice.KeepsCase);

    public string SingularHeading => Heading(Singular);
    public string PluralHeading => Heading(Plural);
    public string SingularInSentence => InSentence(Singular);
    public string PluralInSentence => InSentence(Plural);

    // "1 work", "12 works"
    public string CountOf(int count) => CountText.Of(count, SingularInSentence, PluralInSentence);

    private string Heading(string word) => KeepsCase ? word : char.ToUpperInvariant(word[0]) + word[1..];

    private string InSentence(string word) => KeepsCase ? word : word.ToLowerInvariant();
}
