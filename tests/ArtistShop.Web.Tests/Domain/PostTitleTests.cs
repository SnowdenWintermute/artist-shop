using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Publishing;

namespace ArtistShop.Web.Tests.Domain;

public class PostTitleTests
{
    [Theory]
    [InlineData(1, "Spring show (copy)")]
    [InlineData(2, "Spring show (copy 2)")]
    public void ACopyIsNumberedAfterTheFirst(int number, string expected)
    {
        Assert.Equal(expected, new PostTitle("Spring show").ForCopy(number).Value);
    }

    [Fact]
    public void ALongTitlesCopiesKeepDifferentSlugs()
    {
        var title = new PostTitle(new string('a', ArtistShopLimits.PostTitleMaximumLength));

        var slugs = Enumerable.Range(1, 12).Select(number => PostSlug.FromTitle(title.ForCopy(number).Value));

        Assert.Equal(12, slugs.Distinct().Count());
    }
}
