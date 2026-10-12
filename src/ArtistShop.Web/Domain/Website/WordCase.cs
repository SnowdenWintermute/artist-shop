namespace ArtistShop.Web.Domain.Website;

// How the website cases a word the artist chose: capitalised in a heading and lowercase inside a
// sentence, unless they keep the capitals as typed
public static class WordCase
{
    public static string Heading(string word, bool keepsCase) =>
        keepsCase ? word : char.ToUpperInvariant(word[0]) + word[1..];

    public static string InSentence(string word, bool keepsCase) => keepsCase ? word : word.ToLowerInvariant();
}
