using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;

namespace ArtistShop.Web.Tests.Domain;

public class PostExcerptTests
{
    private static IReadOnlyList<RichTextRun> Plain(string text) => [new RichTextRun(text, false, false, false, null)];

    private static RichTextDocument Document(params RichTextBlock[] blocks) => new(blocks);

    private static readonly WorkEmbedBlock Work = new(
        new WorkId(1),
        "0123456789abcdef0123456789abcdef",
        EmbedImageSize.Medium,
        EmbedLayout.Center,
        "A caption"
    );

    [Fact]
    public void JoinsTheTextOfEveryKindOfBlockAndSkipsEmbeds() =>
        Assert.Equal(
            "Heading Opening line. Quoted One Two",
            PostExcerpt.From(
                Document(
                    new HeadingBlock(HeadingLevel.Two, Plain("Heading")),
                    Work,
                    new ParagraphBlock(Plain("Opening line.")),
                    new ParagraphBlock([]),
                    new BlockquoteBlock(Plain("Quoted")),
                    new ListBlock(ListStyle.Bullet, [Plain("One"), Plain("Two")])
                )
            )
        );

    [Fact]
    public void DropsFormattingAndJoinsARunsText() =>
        Assert.Equal(
            "A bold link here",
            PostExcerpt.From(
                Document(
                    new ParagraphBlock(
                        [
                            new RichTextRun("A ", false, false, false, null),
                            new RichTextRun("bold", true, false, false, null),
                            new RichTextRun(" link", false, false, false, "https://example.com"),
                            new RichTextRun("  \n here", false, false, false, null),
                        ]
                    )
                )
            )
        );

    [Fact]
    public void CutsALongPostOnAWholeWord()
    {
        var excerpt = PostExcerpt.From(Document(new ParagraphBlock(Plain(string.Join(" ", Enumerable.Repeat("word", 100))))));

        Assert.NotNull(excerpt);
        Assert.EndsWith("word…", excerpt);
        Assert.True(excerpt.Length <= PostExcerpt.MaximumLength + 1);
    }

    [Fact]
    public void CutsASingleOverlongWordMidWord()
    {
        var excerpt = PostExcerpt.From(Document(new ParagraphBlock(Plain(new string('a', 400)))));

        Assert.Equal(new string('a', PostExcerpt.MaximumLength) + "…", excerpt);
    }

    [Fact]
    public void AShortPostIsWhole() =>
        Assert.Equal("Just this.", PostExcerpt.From(Document(new ParagraphBlock(Plain("Just this.")))));

    [Fact]
    public void APostWithNoTextHasNone() => Assert.Null(PostExcerpt.From(Document(Work, new ParagraphBlock([]))));
}
