using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Exports;

namespace ArtistShop.Web.Tests.Exports;

public sealed class PostExportHtmlTests
{
    private static Post Draft(string title) =>
        new(new PostId(1), new PostTitle(title), new PostSlug("notes"), PostBody.Empty, PublishedAt: null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    // the page is written as text, not through Razor, so nothing escapes it for us
    [Fact]
    public void TextAndLinksAreEscaped()
    {
        var document = new PostDocument(
            [new ParagraphBlock([new PostText("<script>alert(1)</script>", Bold: false, Italic: false, Underline: false, Link: "https://example.com/?a=1&b=\"2\"")])]
        );

        var page = PostExportHtml.Page(Draft("Tom & <Jerry>"), document, new Dictionary<PostBlock, PostExportImage>(), "https://site.example");

        Assert.DoesNotContain("<script>", page);
        Assert.DoesNotContain("<Jerry>", page);
        Assert.Contains("&lt;script&gt;", page);
        Assert.Contains("href=\"https://example.com/?a=1&amp;b=&quot;2&quot;\"", page);
    }

    [Fact]
    public void ADraftSaysItWasNeverPublished()
    {
        var page = PostExportHtml.Page(Draft("Notes"), new PostDocument([]), new Dictionary<PostBlock, PostExportImage>(), "https://site.example");

        Assert.Contains("Draft, never published.", page);
    }
}
