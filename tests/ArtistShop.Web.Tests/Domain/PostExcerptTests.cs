using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;

namespace ArtistShop.Web.Tests.Domain;

public class PostExcerptTests
{
    private static IReadOnlyList<RichTextRun> Plain(string text) => [new RichTextRun(text, false, false, false, null, TextSize.Size16)];

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
                    new HeadingBlock(Plain("Heading"), TextAlignment.Left),
                    Work,
                    new ParagraphBlock(Plain("Opening line."), TextAlignment.Left),
                    new ParagraphBlock([], TextAlignment.Left),
                    new BlockquoteBlock(Plain("Quoted"), TextAlignment.Left),
                    new ListBlock(ListStyle.Bullet, [Plain("One"), Plain("Two")], TextAlignment.Left)
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
                            new RichTextRun("A ", false, false, false, null, TextSize.Size16),
                            new RichTextRun("bold", true, false, false, null, TextSize.Size16),
                            new RichTextRun(" link", false, false, false, "https://example.com", TextSize.Size16),
                            new RichTextRun("  \n here", false, false, false, null, TextSize.Size16),
                        ],
                        TextAlignment.Left
                    )
                )
            )
        );

    [Fact]
    public void CutsALongPostOnAWholeWord()
    {
        var excerpt = PostExcerpt.From(Document(new ParagraphBlock(Plain(string.Join(" ", Enumerable.Repeat("word", 100))), TextAlignment.Left)));

        Assert.NotNull(excerpt);
        Assert.EndsWith("word…", excerpt);
        Assert.True(excerpt.Length <= PostExcerpt.MaximumLength + 1);
    }

    [Fact]
    public void CutsASingleOverlongWordMidWord()
    {
        var excerpt = PostExcerpt.From(Document(new ParagraphBlock(Plain(new string('a', 400)), TextAlignment.Left)));

        Assert.Equal(new string('a', PostExcerpt.MaximumLength) + "…", excerpt);
    }

    [Fact]
    public void AShortPostIsWhole() =>
        Assert.Equal("Just this.", PostExcerpt.From(Document(new ParagraphBlock(Plain("Just this."), TextAlignment.Left))));

    [Fact]
    public void APostWithNoTextHasNone() => Assert.Null(PostExcerpt.From(Document(Work, new ParagraphBlock([], TextAlignment.Left))));
}
