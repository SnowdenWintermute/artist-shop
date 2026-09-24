using System.Text.Json;
using ArtistShop.Web.Domain.Catalog;
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
    [InlineData("/artworks/some-slug")]
    [InlineData("/")]
    public void KeepsWebMailAndSiteLinks(string link)
    {
        var blocks = Parse(
            $$$"""{"ops":[{"insert":"here","attributes":{"link":{{{JsonSerializer.Serialize(link)}}}}},{"insert":"\n"}]}"""
        );

        Assert.Equal(link, Assert.IsType<ParagraphBlock>(Assert.Single(blocks)).Text[0].Link);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hello")]
    [InlineData("//example.com")]
    [InlineData(@"/\example.com")]
    [InlineData("/\t/example.com")]
    [InlineData("relative")]
    public void DropsOtherLinksButKeepsTheirText(string link)
    {
        var blocks = Parse(
            $$$"""{"ops":[{"insert":"here","attributes":{"link":{{{JsonSerializer.Serialize(link)}}}}},{"insert":"\n"}]}"""
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
                {"insert":{"artshop-artwork":{"artworkId":42,"storageKey":"0192f1c2a3b44c5d8e9f0a1b2c3d4e5f","size":"small","layout":"floatLeft"}}},
                {"insert":"\n"}
            ]}
            """
        );

        Assert.Equal(
            new ArtworkEmbedBlock(new ArtworkId(42), "0192f1c2a3b44c5d8e9f0a1b2c3d4e5f", EmbedImageSize.Small, EmbedLayout.FloatLeft, null),
            blocks[0]
        );
    }

    [Fact]
    public void AnArtworkEmbedDefaultsToMediumAndCentred()
    {
        var blocks = Parse(
            """{"ops":[{"insert":{"artshop-artwork":{"artworkId":42,"storageKey":"0192f1c2a3b44c5d8e9f0a1b2c3d4e5f"}}},{"insert":"\n"}]}"""
        );

        Assert.Equal(
            new ArtworkEmbedBlock(new ArtworkId(42), "0192f1c2a3b44c5d8e9f0a1b2c3d4e5f", EmbedImageSize.Medium, EmbedLayout.Center, null),
            blocks[0]
        );
    }

    [Theory]
    [InlineData("center", EmbedLayout.Center)]
    [InlineData("left", EmbedLayout.Left)]
    [InlineData("right", EmbedLayout.Right)]
    [InlineData("floatLeft", EmbedLayout.FloatLeft)]
    [InlineData("floatRight", EmbedLayout.FloatRight)]
    [InlineData("sideways", EmbedLayout.Center)]
    public void ReadsEachLayoutAndCentresAnUnknownOne(string layout, EmbedLayout expected)
    {
        var blocks = Parse(
            """{"ops":[{"insert":{"artshop-artwork":{"artworkId":42,"storageKey":"0192f1c2a3b44c5d8e9f0a1b2c3d4e5f","layout":"""
                + JsonSerializer.Serialize(layout)
                + """}}},{"insert":"\n"}]}"""
        );

        Assert.Equal(expected, Assert.IsType<ArtworkEmbedBlock>(blocks[0]).Layout);
    }

    [Fact]
    public void DropsAnArtworkEmbedWithNoStorageKey()
    {
        var blocks = Parse("""{"ops":[{"insert":{"artshop-artwork":{"artworkId":42}}},{"insert":"\n"}]}""");

        Assert.IsType<ParagraphBlock>(Assert.Single(blocks));
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("0192f1c2a3b44c5d8e9f0a1b2c3d4e5f\n")]
    public void DropsAnArtworkEmbedWhoseStorageKeyIsNotOneOfOurs(string storageKey)
    {
        var blocks = Parse(
            """{"ops":[{"insert":{"artshop-artwork":{"artworkId":42,"storageKey":"""
                + JsonSerializer.Serialize(storageKey)
                + """}}},{"insert":"\n"}]}"""
        );

        Assert.IsType<ParagraphBlock>(Assert.Single(blocks));
    }

    [Fact]
    public void DropsAnArtworkEmbedWhoseIdIsNotANumber()
    {
        var blocks = Parse("""{"ops":[{"insert":{"artshop-artwork":{"artworkId":"42","storageKey":"0192f1c2a3b44c5d8e9f0a1b2c3d4e5f"}}},{"insert":"\n"}]}""");

        Assert.IsType<ParagraphBlock>(Assert.Single(blocks));
    }

    // the same rule set_post_artworks applies, so the page and "Mentioned in" agree
    [Theory]
    [InlineData("41.5")]
    [InlineData("2147483648")]
    public void DropsAnArtworkEmbedWhoseIdIsNotAnInt(string artworkId)
    {
        var blocks = Parse(
            """{"ops":[{"insert":{"artshop-artwork":{"storageKey":"0192f1c2a3b44c5d8e9f0a1b2c3d4e5f","artworkId":""" + artworkId + """}}},{"insert":"\n"}]}"""
        );

        Assert.IsType<ParagraphBlock>(Assert.Single(blocks));
    }

    [Theory]
    [InlineData("42.0")]
    [InlineData("4.2e1")]
    public void ReadsAWholeArtworkIdWrittenAsADecimal(string artworkId)
    {
        var blocks = Parse(
            """{"ops":[{"insert":{"artshop-artwork":{"storageKey":"0192f1c2a3b44c5d8e9f0a1b2c3d4e5f","artworkId":""" + artworkId + """}}},{"insert":"\n"}]}"""
        );

        Assert.Equal(new ArtworkId(42), Assert.IsType<ArtworkEmbedBlock>(blocks[0]).ArtworkId);
    }

    [Theory]
    [InlineData("\"Study for a portrait\"", "Study for a portrait")]
    [InlineData("\"  Oil on linen, 2019 \"", "Oil on linen, 2019")]
    [InlineData("\"   \"", null)]
    [InlineData("\"\"", null)]
    [InlineData("42", null)]
    public void ReadsAnArtworkEmbedsCaptionTrimmedAndBlankAsNone(string caption, string? expected)
    {
        var blocks = Parse(
            """{"ops":[{"insert":{"artshop-artwork":{"artworkId":42,"storageKey":"0192f1c2a3b44c5d8e9f0a1b2c3d4e5f","caption":"""
                + caption
                + """}}},{"insert":"\n"}]}"""
        );

        Assert.Equal(expected, Assert.IsType<ArtworkEmbedBlock>(blocks[0]).Caption);
    }

    [Fact]
    public void ReadsAYouTubeVideo()
    {
        var blocks = Parse(
            """{"ops":[{"insert":{"artshop-video":{"provider":"youtube","videoId":"dQw4w9WgXcQ","layout":"floatRight"}}},{"insert":"\n"}]}"""
        );

        Assert.Equal(new VideoEmbedBlock(new YouTubeVideo("dQw4w9WgXcQ"), EmbedLayout.FloatRight), blocks[0]);
    }

    [Fact]
    public void ReadsAVimeoVideo()
    {
        var blocks = Parse(
            """{"ops":[{"insert":{"artshop-video":{"provider":"vimeo","videoId":"76979871"}}},{"insert":"\n"}]}"""
        );

        Assert.Equal(new VideoEmbedBlock(new VimeoVideo("76979871", null), EmbedLayout.Center), blocks[0]);
    }

    [Fact]
    public void ReadsAnUnlistedVimeoVideo()
    {
        var blocks = Parse(
            """{"ops":[{"insert":{"artshop-video":{"provider":"vimeo","videoId":"76979871","hash":"8272103f6e"}}},{"insert":"\n"}]}"""
        );

        Assert.Equal(
            new VimeoVideo("76979871", "8272103f6e"),
            Assert.IsType<VideoEmbedBlock>(blocks[0]).Source
        );
    }

    [Theory]
    [InlineData("youtube", "https://www.youtube.com/watch?v=dQw4w9WgXcQ", null)]
    [InlineData("youtube", "dQw4w9WgXc\"", null)]
    [InlineData("youtube", "short", null)]
    [InlineData("vimeo", "dQw4w9WgXcQ", null)]
    [InlineData("vimeo", "76979871?autoplay=1", null)]
    [InlineData("vimeo", "76979871", "8272103f6e&autoplay=1")]
    [InlineData("vimeo", "76979871", "")]
    [InlineData("dailymotion", "x8abc12", null)]
    [InlineData(null, "dQw4w9WgXcQ", null)]
    public void DropsAVideoWhosePartsAreNotItsSitesShape(string? provider, string videoId, string? hash)
    {
        var value = new Dictionary<string, string?> { ["provider"] = provider, ["videoId"] = videoId, ["hash"] = hash }
            .Where(part => part.Value is not null)
            .ToDictionary();

        var blocks = Parse(
            """{"ops":[{"insert":{"artshop-video":"""
                + JsonSerializer.Serialize(value)
                + """}},{"insert":"\n"}]}"""
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
            """{"ops":[{"insert":"Before"},{"insert":{"artshop-video":{"provider":"youtube","videoId":"dQw4w9WgXcQ"}}},{"insert":"\n"}]}"""
        );

        Assert.Equal([Plain("Before")], Assert.IsType<ParagraphBlock>(blocks[0]).Text);
        Assert.IsType<VideoEmbedBlock>(blocks[1]);
    }
}
