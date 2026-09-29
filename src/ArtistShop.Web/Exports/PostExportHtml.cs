using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using ArtistShop.Web.Components.Publishing;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Images;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Exports;

// An image beside a post's page. ImageWidth and ImageHeight are the image's own, for its shape.
// OriginalFileName is the upload a click opens; null for an artwork, whose original is in the
// image download
public record PostExportImage(string FileName, string? OriginalFileName, string Alt, int ImageWidth, int ImageHeight);

// The post download's pages: plain HTML with its styles in the page and no scripts, so a post
// opens from the folder in any browser, and pastes into another blog without our site's classes
public static class PostExportHtml
{
    public const string IndexFileName = "index.html";

    // leaves every character a post may be written in as it is, and escapes only what HTML needs
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    // The editor's look: every line directly below the last, a gap being an empty line the artist
    // left, runs of spaces kept. Below 40rem a wrapped embed takes a centred line of its own, as on
    // the post page
    private const string Style = """
        body { max-width: 42rem; margin: 0 auto; padding: 1rem; font-family: system-ui, sans-serif; line-height: 1.5; }
        article { display: flow-root; overflow-wrap: break-word; }
        article p, article h2, article h3, article blockquote, article ul, article ol { margin: 0; }
        article p, article h2, article h3, article blockquote, article li { white-space: pre-wrap; }
        article ul, article ol { padding-left: 1.5rem; }
        blockquote { border-left: 4px solid #ccc; padding-left: 1rem; }
        figure { margin: 0.5rem 0; max-width: 100%; }
        figure.center { margin-left: auto; margin-right: auto; }
        figure.right { margin-left: auto; }
        figure.floatLeft { float: left; margin-right: 1rem; }
        figure.floatRight { float: right; margin-left: 1rem; }
        figure img { display: block; width: 100%; height: auto; }
        figcaption { padding-top: 0.25rem; font-size: 0.875rem; color: #666; }
        .quiet { color: #666; }
        @media (max-width: 40rem) {
          figure.floatLeft, figure.floatRight { float: none; margin-left: auto; margin-right: auto; }
        }
        """;

    // each post is in a folder named by its slug, which is unique and a portable file name
    public static string PagePath(Post post) => $"{post.Slug.Value}/{post.Slug.Value}.html";

    // Images is the file for each image embed there is one for; an embed missing from it is left
    // out, as the post page leaves out one whose image is gone. A link to a path on the site gets
    // siteOrigin in front, since it would point nowhere from the folder
    public static string Page(
        Post post,
        PostDocument document,
        IReadOnlyDictionary<PostBlock, PostExportImage> images,
        string siteOrigin
    )
    {
        var html = new StringBuilder();
        StartPage(html, post.Title.Value);

        html.Append($"""<p><a href="../{IndexFileName}">All posts</a></p>""").Append('\n');

        if (post.PublishedAt is { } publishedAt)
        {
            html.Append($"<h1>{Encode(post.Title.Value)}</h1>\n");
            html.Append($"""<p class="quiet">{Time(publishedAt)}</p>""").Append('\n');
        }
        else
        {
            html.Append("""<p class="quiet">Draft, never published.</p>""").Append('\n');
            html.Append($"<h1>{Encode(post.Title.Value)}</h1>\n");
        }

        html.Append("<article>\n");

        foreach (var block in document.Blocks)
        {
            AppendBlock(html, block, images, siteOrigin);
        }

        html.Append("</article>\n");
        EndPage(html);

        return html.ToString();
    }

    // published posts newest first, then drafts, most recently changed first
    public static string Index(string host, IReadOnlyList<Post> posts)
    {
        var html = new StringBuilder();
        StartPage(html, $"Posts from {host}");

        html.Append($"<h1>Posts from {Encode(host)}</h1>\n");

        var published = posts.Where(post => post.PublishedAt is not null).OrderByDescending(post => post.PublishedAt).ToList();
        var drafts = posts.Where(post => post.PublishedAt is null).OrderByDescending(post => post.UpdatedAt).ToList();

        if (published.Count > 0)
        {
            html.Append("<ul>\n");

            foreach (var post in published)
            {
                html.Append($"""<li><a href="{Encode(PagePath(post))}">{Encode(post.Title.Value)}</a> <span class="quiet">{Time(Unwrap.Value(post.PublishedAt))}</span></li>""").Append('\n');
            }

            html.Append("</ul>\n");
        }

        if (drafts.Count > 0)
        {
            html.Append("<h2>Drafts</h2>\n<ul>\n");

            foreach (var post in drafts)
            {
                html.Append($"""<li><a href="{Encode(PagePath(post))}">{Encode(post.Title.Value)}</a></li>""").Append('\n');
            }

            html.Append("</ul>\n");
        }

        EndPage(html);

        return html.ToString();
    }

    private static void StartPage(StringBuilder html, string title) =>
        html.Append(
            $"""
            <!doctype html>
            <html>
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{Encode(title)}</title>
            <style>
            {Style}
            </style>
            </head>
            <body>

            """
        );

    private static void EndPage(StringBuilder html) => html.Append("</body>\n</html>\n");

    private static void AppendBlock(
        StringBuilder html,
        PostBlock block,
        IReadOnlyDictionary<PostBlock, PostExportImage> images,
        string siteOrigin
    )
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                html.Append($"<p>{Inline(paragraph.Text, siteOrigin)}</p>\n");
                break;

            case HeadingBlock { Level: HeadingLevel.Two } heading:
                html.Append($"<h2>{Inline(heading.Text, siteOrigin)}</h2>\n");
                break;

            case HeadingBlock heading:
                html.Append($"<h3>{Inline(heading.Text, siteOrigin)}</h3>\n");
                break;

            case BlockquoteBlock quote:
                html.Append($"<blockquote>{Inline(quote.Text, siteOrigin)}</blockquote>\n");
                break;

            case ListBlock list:
                var tag = list.Style is ListStyle.Bullet ? "ul" : "ol";
                html.Append($"<{tag}>\n");

                foreach (var item in list.Items)
                {
                    html.Append($"<li>{Inline(item, siteOrigin)}</li>\n");
                }

                html.Append($"</{tag}>\n");
                break;

            case ArtworkEmbedBlock embed when images.TryGetValue(embed, out var image):
                AppendFigure(html, image, embed.Size, embed.Layout, embed.Caption);
                break;

            case PostImageEmbedBlock embed when images.TryGetValue(embed, out var image):
                AppendFigure(html, image, embed.Size, embed.Layout, embed.Caption);
                break;

            case VideoEmbedBlock video:
                var url = Encode(VideoUrls.Page(video.Source));
                html.Append($"""<p><a href="{url}">{url}</a></p>""").Append('\n');
                break;
        }
    }

    // as wide as the size picked, the image's own shape
    private static void AppendFigure(StringBuilder html, PostExportImage image, EmbedImageSize size, EmbedLayout layout, string? caption)
    {
        var width = ImageVariants.EmbedWidth(size);
        var height = (int)Math.Round((double)width * image.ImageHeight / image.ImageWidth);
        var img =
            $"""<img src="{Encode(image.FileName)}" alt="{Encode(image.Alt)}" width="{Number(width)}" height="{Number(height)}">""";

        html.Append($"""<figure class="{EmbedLayoutNames.Of(layout)}" style="width: {Number(width)}px">""");
        html.Append(image.OriginalFileName is null ? img : $"""<a href="{Encode(image.OriginalFileName)}">{img}</a>""");

        if (caption is not null)
        {
            html.Append($"<figcaption>{Encode(caption)}</figcaption>");
        }

        html.Append("</figure>\n");
    }

    // each wrapper goes around the one before it, as on the post page: <a><strong><em><u>
    private static string Inline(IReadOnlyList<PostText> text, string siteOrigin)
    {
        if (text.Count == 0)
        {
            return "<br>";
        }

        var html = new StringBuilder();

        foreach (var run in text)
        {
            var content = Encode(run.Text);

            if (run.Underline)
            {
                content = $"<u>{content}</u>";
            }

            if (run.Italic)
            {
                content = $"<em>{content}</em>";
            }

            if (run.Bold)
            {
                content = $"<strong>{content}</strong>";
            }

            if (run.Link is string link)
            {
                var href = link.StartsWith('/') ? siteOrigin + link : link;
                content = $"""<a href="{Encode(href)}">{content}</a>""";
            }

            html.Append(content);
        }

        return html.ToString();
    }

    // The day as it falls in UTC, since there's no script to show the reader's own day as the site
    // does; the moment itself is in the datetime
    private static string Time(DateTimeOffset moment) =>
        $"""<time datetime="{DateText.DateTimeAttribute(moment)}">{DateText.Day(moment)}</time>""";

    private static string Encode(string text) => Encoder.Encode(text);

    private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);
}
