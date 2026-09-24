using ArtistShop.Web.Domain.Publishing;

namespace ArtistShop.Web.Tests.Domain;

public class VideoSourcesTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?feature=shared&v=dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ&t=42s")]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=abc123")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData("  youtu.be/dQw4w9WgXcQ  ")]
    [InlineData("http://WWW.YouTube.com/watch?v=dQw4w9WgXcQ")]
    public void ReadsAYouTubeLink(string link) =>
        Assert.Equal(new YouTubeVideo("dQw4w9WgXcQ"), VideoSources.FromLink(link));

    [Theory]
    [InlineData("https://vimeo.com/76979871", null)]
    [InlineData("vimeo.com/76979871", null)]
    [InlineData("https://vimeo.com/76979871/8272103f6e", "8272103f6e")]
    [InlineData("https://vimeo.com/76979871/12345", "12345")]
    [InlineData("https://player.vimeo.com/video/76979871", null)]
    [InlineData("https://player.vimeo.com/video/76979871?h=8272103f6e", "8272103f6e")]
    [InlineData("https://vimeo.com/channels/staffpicks/76979871", null)]
    [InlineData("https://vimeo.com/showcase/11111/video/76979871", null)]
    public void ReadsAVimeoLink(string link, string? unlistedHash) =>
        Assert.Equal(new VimeoVideo("76979871", unlistedHash), VideoSources.FromLink(link));

    [Theory]
    [InlineData("")]
    [InlineData("dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=short")]
    [InlineData("https://www.youtube.com/@somechannel")]
    [InlineData("https://notyoutube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com.example.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("ftp://youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://vimeo.com/channels/staffpicks")]
    [InlineData("https://vimeo.com/76979871/not-a-hash")]
    [InlineData("https://dailymotion.com/video/x8abc12")]
    public void ReadsNoVideoFromAnythingElse(string link) => Assert.Null(VideoSources.FromLink(link));
}
