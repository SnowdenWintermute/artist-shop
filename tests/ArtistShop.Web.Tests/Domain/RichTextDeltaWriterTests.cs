using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;

namespace ArtistShop.Web.Tests.Domain;

public class RichTextDeltaWriterTests
{
    private const string StorageKey = "0123456789abcdef0123456789abcdef";

    // every format and embed the editor writes, in the shape Quill stores it
    private const string EveryFormat = """
        {"ops":[
          {"insert":"plain "},{"insert":"bold","attributes":{"bold":true}},{"insert":" and "},
          {"insert":"all at once","attributes":{"bold":true,"italic":true,"underline":true,"link":"https://example.com/"}},
          {"insert":"\n"},
          {"insert":"\n"},
          {"insert":"A heading"},{"insert":"\n","attributes":{"artshop-heading":true}},
          {"insert":"Large","attributes":{"size":"72","bold":true}},{"insert":" and tiny","attributes":{"size":"14"}},{"insert":"\n","attributes":{"artshop-heading":true}},
          {"insert":"Sized text","attributes":{"size":"24"}},{"insert":"\n"},
          {"insert":"Centred"},{"insert":"\n","attributes":{"align":"center"}},
          {"insert":"Right heading"},{"insert":"\n","attributes":{"artshop-heading":true,"align":"right"}},
          {"insert":"Centred quote"},{"insert":"\n","attributes":{"blockquote":true,"align":"center"}},
          {"insert":"Centred item"},{"insert":"\n","attributes":{"list":"bullet","align":"center"}},
          {"insert":"Quoted"},{"insert":"\n","attributes":{"blockquote":true}},
          {"insert":"One"},{"insert":"\n","attributes":{"list":"bullet"}},
          {"insert":"Two","attributes":{"link":"/collections/gardens"}},{"insert":"\n","attributes":{"list":"bullet"}},
          {"insert":"First"},{"insert":"\n","attributes":{"list":"ordered"}},
          {"insert":{"artshop-work":{"workId":7,"storageKey":"0123456789abcdef0123456789abcdef","size":"small","layout":"floatLeft","caption":"Dawn"}}},
          {"insert":{"artshop-image":{"storageKey":"0123456789abcdef0123456789abcdef","width":1200,"height":800,"blur":"data:image/webp;base64,AAAA","size":"medium","layout":"floatRight","caption":"A study","alt":"Pencil","lightbox":true}}},
          {"insert":{"artshop-video":{"provider":"youtube","videoId":"dQw4w9WgXcQ","layout":"center"}}},
          {"insert":{"artshop-video":{"provider":"vimeo","videoId":"76979871","hash":"abc123","layout":"floatLeft"}}},
          {"insert":"The end\n"}
        ]}
        """;

    [Fact]
    public void ParsingWhatItWritesGivesTheSameDocument()
    {
        var document = RichTextParser.Parse(new RichText(EveryFormat));

        var written = RichTextDeltaWriter.Write(document);

        Assert.Equivalent(document, RichTextParser.Parse(written), strict: true);
    }

    [Fact]
    public void RewritingGivesOlderTextsHeadingsTheirSizes()
    {
        var older = new RichText("""{"ops":[{"insert":"Title"},{"insert":"\n","attributes":{"header":1}}]}""");

        Assert.Equal(
            """{"ops":[{"insert":"Title","attributes":{"bold":true,"size":"36"}},{"insert":"\n","attributes":{"artshop-heading":true}}]}""",
            RichTextDeltaWriter.Rewrite(older).Json
        );
    }

    [Fact]
    public void ADocumentEndingInAnEmbedStillEndsWithALine()
    {
        var document = new RichTextDocument(
            [new VideoEmbedBlock(new YouTubeVideo("dQw4w9WgXcQ"), EmbedLayout.Center)]
        );

        var written = RichTextDeltaWriter.Write(document);

        Assert.EndsWith("""{"insert":"\n"}]}""", written.Json);
    }

    [Fact]
    public void AnEmptyDocumentIsTheEditorsEmptyOne()
    {
        Assert.Equal(RichText.Empty.Json, RichTextDeltaWriter.Write(new RichTextDocument([])).Json);
    }

    [Fact]
    public void ALeftOutEmbedIsNotWritten()
    {
        var document = new RichTextDocument(
            [
                new WorkEmbedBlock(new WorkId(1), StorageKey, EmbedImageSize.Medium, EmbedLayout.Center, Caption: null),
            ]
        );

        var delta = RichTextDeltaWriter.Delta(document, _ => null);

        Assert.Equal(RichText.Empty.Json, delta.ToJsonString());
    }
}
