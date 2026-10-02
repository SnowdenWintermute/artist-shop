using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;

namespace ArtistShop.Web.Tests.Domain;

public class PostDeltaWriterTests
{
    private const string StorageKey = "0123456789abcdef0123456789abcdef";

    // every format and embed the editor writes, in the shape Quill stores it
    private const string EveryFormat = """
        {"ops":[
          {"insert":"plain "},{"insert":"bold","attributes":{"bold":true}},{"insert":" and "},
          {"insert":"all at once","attributes":{"bold":true,"italic":true,"underline":true,"link":"https://example.com/"}},
          {"insert":"\n"},
          {"insert":"\n"},
          {"insert":"A heading"},{"insert":"\n","attributes":{"header":2}},
          {"insert":"A smaller one"},{"insert":"\n","attributes":{"header":3}},
          {"insert":"Quoted"},{"insert":"\n","attributes":{"blockquote":true}},
          {"insert":"One"},{"insert":"\n","attributes":{"list":"bullet"}},
          {"insert":"Two","attributes":{"link":"/series/gardens"}},{"insert":"\n","attributes":{"list":"bullet"}},
          {"insert":"First"},{"insert":"\n","attributes":{"list":"ordered"}},
          {"insert":{"artshop-artwork":{"artworkId":7,"storageKey":"0123456789abcdef0123456789abcdef","size":"small","layout":"floatLeft","caption":"Dawn"}}},
          {"insert":{"artshop-image":{"storageKey":"0123456789abcdef0123456789abcdef","width":1200,"height":800,"blur":"data:image/webp;base64,AAAA","size":"medium","layout":"floatRight","caption":"A study","alt":"Pencil","lightbox":true}}},
          {"insert":{"artshop-video":{"provider":"youtube","videoId":"dQw4w9WgXcQ","layout":"center"}}},
          {"insert":{"artshop-video":{"provider":"vimeo","videoId":"76979871","hash":"abc123","layout":"floatLeft"}}},
          {"insert":"The end\n"}
        ]}
        """;

    [Fact]
    public void ParsingWhatItWritesGivesTheSameDocument()
    {
        var document = PostDocumentParser.Parse(new PostBody(EveryFormat));

        var written = PostDeltaWriter.Write(document);

        Assert.Equivalent(document, PostDocumentParser.Parse(written), strict: true);
    }

    [Fact]
    public void ADocumentEndingInAnEmbedStillEndsWithALine()
    {
        var document = new PostDocument(
            [new VideoEmbedBlock(new YouTubeVideo("dQw4w9WgXcQ"), EmbedLayout.Center)]
        );

        var written = PostDeltaWriter.Write(document);

        Assert.EndsWith("""{"insert":"\n"}]}""", written.Json);
    }

    [Fact]
    public void AnEmptyDocumentIsTheEditorsEmptyOne()
    {
        Assert.Equal(PostBody.Empty.Json, PostDeltaWriter.Write(new PostDocument([])).Json);
    }

    [Fact]
    public void ALeftOutEmbedIsNotWritten()
    {
        var document = new PostDocument(
            [
                new ArtworkEmbedBlock(new ArtworkId(1), StorageKey, EmbedImageSize.Medium, EmbedLayout.Center, Caption: null),
            ]
        );

        var delta = PostDeltaWriter.Delta(document, _ => null);

        Assert.Equal(PostBody.Empty.Json, delta.ToJsonString());
    }
}
