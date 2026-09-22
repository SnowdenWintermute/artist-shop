using System.Text.Json;
using ArtistShop.Web.Domain.Publishing;

namespace ArtistShop.Web.Tests.Domain;

public class PostDocumentParserTests
{
    private static IReadOnlyList<PostBlock> Parse(string json) =>
        PostDocumentParser.Parse(new PostBody(json)).Blocks;

    private static PostText Plain(string text) => new(text, false, false, false, null);

    [Fact]
    public void EachLineIsAParagraph()
    {
        var blocks = Parse("""{"ops":[{"insert":"One\nTwo\n"}]}""");

        Assert.Collection(
            blocks,
            block => Assert.Equal([Plain("One")], Assert.IsType<ParagraphBlock>(block).Text),
            block => Assert.Equal([Plain("Two")], Assert.IsType<ParagraphBlock>(block).Text)
        );
    }

    [Fact]
    public void KeepsABlankLineAsAnEmptyParagraph()
    {
        var blocks = Parse("""{"ops":[{"insert":"One\n\nTwo\n"}]}""");

        Assert.Empty(Assert.IsType<ParagraphBlock>(blocks[1]).Text);
    }

    [Fact]
    public void KeepsInlineFormatsOnTheirOwnRun()
    {
        var blocks = Parse(
            """
            {"ops":[
                {"insert":"plain "},
                {"insert":"bold","attributes":{"bold":true,"underline":true}},
                {"insert":" "},
                {"insert":"italic","attributes":{"italic":true}},
                {"insert":"\n"}
            ]}
            """
        );

        Assert.Equal(
            [
                Plain("plain "),
                new PostText("bold", Bold: true, Italic: false, Underline: true, Link: null),
                Plain(" "),
                new PostText("italic", Bold: false, Italic: true, Underline: false, Link: null),
            ],
            Assert.IsType<ParagraphBlock>(Assert.Single(blocks)).Text
        );
    }

    [Fact]
    public void TheLineFormatComesFromItsNewline()
    {
        var blocks = Parse(
            """{"ops":[{"insert":"Intro\nTitle"},{"insert":"\n","attributes":{"header":2}},{"insert":"Body\n"}]}"""
        );

        Assert.IsType<ParagraphBlock>(blocks[0]);
        var heading = Assert.IsType<HeadingBlock>(blocks[1]);
        Assert.Equal(HeadingLevel.Two, heading.Level);
        Assert.Equal([Plain("Title")], heading.Text);
        Assert.IsType<ParagraphBlock>(blocks[2]);
    }

    [Theory]
    [InlineData(1, HeadingLevel.Two)]
    [InlineData(3, HeadingLevel.Three)]
    [InlineData(4, HeadingLevel.Three)]
    public void FoldsOtherHeadingLevelsIntoTheTwoOffered(int level, HeadingLevel expected)
    {
        var blocks = Parse(
            $$$"""{"ops":[{"insert":"Title"},{"insert":"\n","attributes":{"header":{{{level}}}}}]}"""
        );

        Assert.Equal(expected, Assert.IsType<HeadingBlock>(Assert.Single(blocks)).Level);
    }

    [Fact]
    public void ConsecutiveListLinesOfOneStyleAreOneList()
    {
        var blocks = Parse(
            """
            {"ops":[
                {"insert":"a"},{"insert":"\n","attributes":{"list":"bullet"}},
                {"insert":"b"},{"insert":"\n","attributes":{"list":"bullet"}},
                {"insert":"c"},{"insert":"\n","attributes":{"list":"ordered"}}
            ]}
            """
        );

        Assert.Collection(
            blocks,
            block =>
            {
                var list = Assert.IsType<ListBlock>(block);
                Assert.Equal(ListStyle.Bullet, list.Style);
                Assert.Equal(2, list.Items.Count);
            },
            block => Assert.Equal(ListStyle.Ordered, Assert.IsType<ListBlock>(block).Style)
        );
    }

    [Fact]
    public void AParagraphBetweenListLinesStartsANewList()
    {
        var blocks = Parse(
            """
            {"ops":[
                {"insert":"a"},{"insert":"\n","attributes":{"list":"bullet"}},
                {"insert":"between\n"},
                {"insert":"b"},{"insert":"\n","attributes":{"list":"bullet"}}
            ]}
            """
        );

        Assert.Equal(3, blocks.Count);
        Assert.IsType<ListBlock>(blocks[2]);
    }

    [Fact]
    public void ReadsABlockquote()
    {
        var blocks = Parse(
            """{"ops":[{"insert":"Said"},{"insert":"\n","attributes":{"blockquote":true}}]}"""
        );

        Assert.Equal([Plain("Said")], Assert.IsType<BlockquoteBlock>(Assert.Single(blocks)).Text);
    }

    [Theory]
    [InlineData("https://example.com/a")]
    [InlineData("http://example.com")]
    [InlineData("mailto:someone@example.com")]
    public void KeepsWebAndMailLinks(string link)
    {
        var blocks = Parse(
            $$$"""{"ops":[{"insert":"here","attributes":{"link":"{{{link}}}"}},{"insert":"\n"}]}"""
        );

        Assert.Equal(link, Assert.IsType<ParagraphBlock>(Assert.Single(blocks)).Text[0].Link);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hello")]
    [InlineData("/relative")]
    public void DropsOtherLinksButKeepsTheirText(string link)
    {
        var blocks = Parse(
            $$$"""{"ops":[{"insert":"here","attributes":{"link":"{{{link}}}"}},{"insert":"\n"}]}"""
        );

        Assert.Equal([Plain("here")], Assert.IsType<ParagraphBlock>(Assert.Single(blocks)).Text);
    }

    [Fact]
    public void DropsFormatsItDoesNotKnow()
    {
        var blocks = Parse(
            """{"ops":[{"insert":"red","attributes":{"color":"#f00","font":"serif"}},{"insert":"\n","attributes":{"align":"center"}}]}"""
        );

        Assert.Equal([Plain("red")], Assert.IsType<ParagraphBlock>(Assert.Single(blocks)).Text);
    }

    [Fact]
    public void ReadsAnArtworkEmbed()
    {
        var blocks = Parse(
            """
            {"ops":[
                {"insert":{"artwork":{"artworkId":42,"storageKey":"0192f1c2a3b44c5d8e9f0a1b2c3d4e5f","size":"small","layout":"floatLeft"}}},
                {"insert":"\n"}
            ]}
            """
        );

        Assert.Equal(
            new ArtworkEmbedBlock(42, "0192f1c2a3b44c5d8e9f0a1b2c3d4e5f", EmbedImageSize.Small, EmbedLayout.FloatLeft),
            blocks[0]
        );
    }

    [Fact]
    public void AnArtworkEmbedDefaultsToTheMediumPrimaryImageCentred()
    {
        var blocks = Parse("""{"ops":[{"insert":{"artwork":{"artworkId":42}}},{"insert":"\n"}]}""");

        Assert.Equal(
            new ArtworkEmbedBlock(42, null, EmbedImageSize.Medium, EmbedLayout.Center),
            blocks[0]
        );
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("0192f1c2a3b44c5d8e9f0a1b2c3d4e5f\n")]
    public void IgnoresAStorageKeyThatIsNotOneOfOurs(string storageKey)
    {
        var blocks = Parse(
            """{"ops":[{"insert":{"artwork":{"artworkId":42,"storageKey":"""
                + JsonSerializer.Serialize(storageKey)
                + """}}},{"insert":"\n"}]}"""
        );

        Assert.Null(Assert.IsType<ArtworkEmbedBlock>(blocks[0]).StorageKey);
    }

    [Fact]
    public void DropsAnArtworkEmbedWithNoId()
    {
        var blocks = Parse("""{"ops":[{"insert":{"artwork":{"artworkId":"42"}}},{"insert":"\n"}]}""");

        Assert.IsType<ParagraphBlock>(Assert.Single(blocks));
    }

    [Fact]
    public void ReadsAYouTubeEmbed()
    {
        var blocks = Parse(
            """{"ops":[{"insert":{"youtube":{"videoId":"dQw4w9WgXcQ","layout":"floatRight"}}},{"insert":"\n"}]}"""
        );

        Assert.Equal(new YouTubeEmbedBlock("dQw4w9WgXcQ", EmbedLayout.FloatRight), blocks[0]);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("dQw4w9WgXc\"")]
    [InlineData("short")]
    public void DropsAYouTubeEmbedWhoseIdIsNotAnId(string videoId)
    {
        var blocks = Parse(
            """{"ops":[{"insert":{"youtube":{"videoId":"""
                + JsonSerializer.Serialize(videoId)
                + """}}},{"insert":"\n"}]}"""
        );

        Assert.IsType<ParagraphBlock>(Assert.Single(blocks));
    }

    [Fact]
    public void DropsEmbedsItDoesNotKnow()
    {
        var blocks = Parse(
            """{"ops":[{"insert":{"image":"https://example.com/a.png"}},{"insert":"\n"}]}"""
        );

        Assert.IsType<ParagraphBlock>(Assert.Single(blocks));
    }

    [Fact]
    public void TextBeforeAnEmbedOnTheSameLineKeepsItsOwnParagraph()
    {
        var blocks = Parse(
            """{"ops":[{"insert":"Before"},{"insert":{"youtube":{"videoId":"dQw4w9WgXcQ"}}},{"insert":"\n"}]}"""
        );

        Assert.Equal([Plain("Before")], Assert.IsType<ParagraphBlock>(blocks[0]).Text);
        Assert.IsType<YouTubeEmbedBlock>(blocks[1]);
    }
}
