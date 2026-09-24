using System.Text.Json;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Publishing;

namespace ArtistShop.Web.Tests.Publishing;

public class VideoLinkEndpointsTests
{
    // the endpoint hands the editor these parts, and the editor stores them as they come, so
    // what it sends must be what the parser reads back
    [Theory]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://vimeo.com/76979871")]
    [InlineData("https://vimeo.com/76979871/8272103f6e")]
    public void TheEndpointsPartsParseBackToTheSameVideo(string link)
    {
        var source = VideoSources.FromLink(link);
        Assert.NotNull(source);

        // the JSON settings ASP.NET Core writes an endpoint's result with
        var parts = JsonSerializer.Serialize(VideoLinkEndpoints.ToParts(source), JsonSerializerOptions.Web);
        var blocks = PostDocumentParser.Parse(
            new PostBody("""{"ops":[{"insert":{"artshop-video":""" + parts + """}},{"insert":"\n"}]}""")
        ).Blocks;

        Assert.Equal(new VideoEmbedBlock(source, EmbedLayout.Center), blocks[0]);
    }

    [Fact]
    public void LeavesOutTheHashWhenThereIsNone() =>
        Assert.Equal(
            """{"provider":"vimeo","videoId":"76979871"}""",
            JsonSerializer.Serialize(VideoLinkEndpoints.ToParts(new VimeoVideo("76979871", null)), JsonSerializerOptions.Web)
        );
}
