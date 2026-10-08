using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Tests.Domain;

public class DisplayNounTests
{
    [Fact]
    public void TheDefaultsAreCollectionsAndWorks()
    {
        var wording = SiteWording.Default;

        Assert.Equal(("Collections", "collections"), (wording.Collection.PluralHeading, wording.Collection.PluralInSentence));
        Assert.Equal(("work", "works"), (wording.Work.SingularInSentence, wording.Work.PluralInSentence));
    }

    // only the first letter is raised, so the rest of a phrase reads as typed
    [Fact]
    public void AHeadingCapitalisesTheFirstLetterAndASentenceLowersTheWord()
    {
        var noun = new DisplayNoun("art piece", "Art Pieces", KeepsCase: false);

        Assert.Equal(("Art piece", "Art Pieces"), (noun.SingularHeading, noun.PluralHeading));
        Assert.Equal(("art piece", "art pieces"), (noun.SingularInSentence, noun.PluralInSentence));
    }

    [Fact]
    public void KeepingCaseLeavesTheWordAsTyped()
    {
        var noun = new DisplayNoun("NFT", "NFTs", KeepsCase: true);

        Assert.Equal(("NFTs", "NFTs"), (noun.PluralHeading, noun.PluralInSentence));
    }
}
