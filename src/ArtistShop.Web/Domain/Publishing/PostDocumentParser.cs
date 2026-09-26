namespace ArtistShop.Web.Domain.Publishing;

using System.Text.Json;
using System.Text.RegularExpressions;
using ArtistShop.Web.Domain.Catalog;

// Reads the editor's Delta into a PostDocument. A Delta is a flat list of ops, not a tree: text
// arrives in runs carrying their own bold/italic/link, and a line's heading, list or quote format
// rides on the "\n" that ends the line. Anything not recognised is dropped rather than refused,
// so a post keeps showing what it can if the editor ever stores something new
public static partial class PostDocumentParser
{
    // the names the editor registers its embeds under. Prefixed so they can't be mistaken for, or
    // collide with, one of Quill's own formats. set_post_artworks reads the artwork one too
    private const string ArtworkEmbedName = "artshop-artwork";
    private const string PostImageEmbedName = "artshop-image";
    private const string VideoEmbedName = "artshop-video";

    public static PostDocument Parse(PostBody body) => Read(body, droppedLinks: []);

    // The links Parse drops, each once, in the order they appear: anything but a page on this site,
    // http, https or mailto, such as "#section" (headings have no ids to jump to) or "javascript:".
    // Saving refuses a post with any, so none disappears without a word. Found by the same walk as
    // Parse, so the two can't disagree
    public static List<string> LinksDropped(PostBody body)
    {
        var droppedLinks = new List<string>();
        Read(body, droppedLinks);
        return [.. droppedLinks.Distinct()];
    }

    // each link it drops is added to droppedLinks
    private static PostDocument Read(PostBody body, List<string> droppedLinks)
    {
        using var document = JsonDocument.Parse(body.Json);
        var blocks = new List<PostBlock>();
        var line = new List<PostText>();

        if (
            !document.RootElement.TryGetProperty("ops", out var ops)
            || ops.ValueKind is not JsonValueKind.Array
        )
        {
            return new PostDocument(blocks);
        }

        foreach (var op in ops.EnumerateArray())
        {
            if (op.ValueKind is not JsonValueKind.Object || !op.TryGetProperty("insert", out var insert))
            {
                continue;
            }

            JsonElement? attributes =
                op.TryGetProperty("attributes", out var found)
                && found.ValueKind is JsonValueKind.Object
                    ? found
                    : null;

            if (insert.ValueKind is JsonValueKind.String)
            {
                AddText(blocks, line, insert.GetString() ?? "", attributes, droppedLinks);
            }
            else if (insert.ValueKind is JsonValueKind.Object && ReadEmbed(insert) is { } embed)
            {
                // Quill never puts an embed mid-line, but if one arrives there the text before it
                // still gets its own paragraph
                if (line.Count > 0)
                {
                    EndLine(blocks, line, attributes: null);
                }

                blocks.Add(embed);
            }
        }

        // a Delta always ends with "\n", so this only keeps text a malformed one left hanging
        if (line.Count > 0)
        {
            EndLine(blocks, line, attributes: null);
        }

        return new PostDocument(blocks);
    }

    private static void AddText(
        List<PostBlock> blocks,
        List<PostText> line,
        string text,
        JsonElement? attributes,
        List<string> droppedLinks
    )
    {
        var segments = text.Split('\n');

        for (var index = 0; index < segments.Length; index++)
        {
            // every segment but the first was preceded by a "\n", which ended the line before it
            if (index > 0)
            {
                EndLine(blocks, line, attributes);
            }

            if (segments[index].Length > 0)
            {
                var link = GetString(attributes, "link");
                var safeLink = SafeLink(link);

                if (link is not null && safeLink is null)
                {
                    droppedLinks.Add(link);
                }

                line.Add(
                    new PostText(
                        segments[index],
                        IsTrue(attributes, "bold"),
                        IsTrue(attributes, "italic"),
                        IsTrue(attributes, "underline"),
                        safeLink
                    )
                );
            }
        }
    }

    private static void EndLine(List<PostBlock> blocks, List<PostText> line, JsonElement? attributes)
    {
        List<PostText> text = [.. line];
        line.Clear();

        if (GetInt(attributes, "header") is int level)
        {
            // the toolbar offers two levels, but a pasted h1 or h4 keeps its header, so it is
            // folded into the nearer one rather than lost
            blocks.Add(new HeadingBlock(level <= 2 ? HeadingLevel.Two : HeadingLevel.Three, text));
        }
        else if (ReadListStyle(GetString(attributes, "list")) is ListStyle style)
        {
            // consecutive lines of one style are one list
            if (blocks is [.., ListBlock previous] && previous.Style == style)
            {
                blocks[^1] = previous with { Items = [.. previous.Items, text] };
            }
            else
            {
                blocks.Add(new ListBlock(style, [text]));
            }
        }
        else if (IsTrue(attributes, "blockquote"))
        {
            blocks.Add(new BlockquoteBlock(text));
        }
        else
        {
            blocks.Add(new ParagraphBlock(text));
        }
    }

    private static PostBlock? ReadEmbed(JsonElement insert)
    {
        if (insert.TryGetProperty(ArtworkEmbedName, out var artwork) && artwork.ValueKind is JsonValueKind.Object)
        {
            if (GetInt(artwork, "artworkId") is not int artworkId)
            {
                return null;
            }

            var storageKey = GetString(artwork, "storageKey");

            // it becomes part of an image address, so it must look like one of ours
            if (storageKey is null || !StorageKeyPattern().IsMatch(storageKey))
            {
                return null;
            }

            return new ArtworkEmbedBlock(
                new ArtworkId(artworkId),
                storageKey,
                ReadSize(artwork),
                EmbedLayoutNames.Parse(GetString(artwork, "layout")),
                ReadCaption(artwork)
            );
        }

        if (insert.TryGetProperty(PostImageEmbedName, out var image) && image.ValueKind is JsonValueKind.Object)
        {
            var storageKey = GetString(image, "storageKey");

            if (
                storageKey is null
                || !StorageKeyPattern().IsMatch(storageKey)
                || GetInt(image, "width") is not (> 0 and var width)
                || GetInt(image, "height") is not (> 0 and var height)
            )
            {
                return null;
            }

            // A style's url(), so it must be exactly the processor's kind of blur. One that isn't
            // loses only the blur
            var blur = GetString(image, "blur");

            return new PostImageEmbedBlock(
                storageKey,
                width,
                height,
                blur is { Length: <= MaximumBlurLength } && BlurPattern().IsMatch(blur) ? blur : null,
                ReadSize(image),
                EmbedLayoutNames.Parse(GetString(image, "layout")),
                ReadCaption(image),
                GetString(image, "alt")?.Trim() ?? "",
                IsTrue(image, "lightbox")
            );
        }

        if (insert.TryGetProperty(VideoEmbedName, out var video) && video.ValueKind is JsonValueKind.Object)
        {
            return VideoSources.FromParts(GetString(video, "provider"), GetString(video, "videoId"), GetString(video, "hash"))
                is { } source
                ? new VideoEmbedBlock(source, EmbedLayoutNames.Parse(GetString(video, "layout")))
                : null;
        }

        return null;
    }

    private static EmbedImageSize ReadSize(JsonElement embed) =>
        GetString(embed, "size") is "small" ? EmbedImageSize.Small : EmbedImageSize.Medium;

    // stored as typed, so the editor never trims a space from under the artist's cursor
    private static string? ReadCaption(JsonElement embed) =>
        GetString(embed, "caption") is { } caption && !string.IsNullOrWhiteSpace(caption) ? caption.Trim() : null;

    private static ListStyle? ReadListStyle(string? value) =>
        value switch
        {
            "bullet" => ListStyle.Bullet,
            "ordered" => ListStyle.Ordered,
            _ => null,
        };

    // Blazor encodes an href's characters but will still write javascript:alert(1) into one, so
    // the scheme is checked here
    private static string? SafeLink(string? link) =>
        link is not null && IsSitePath(link)
        || Uri.TryCreate(link, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto)
            ? link
            : null;

    // A page on this site, like /artworks/some-slug. Browsers read "//host" and "/\host" as another
    // site's address, and strip tabs and newlines before reading one, so a tab between the two
    // slashes is the same trap
    private static bool IsSitePath(string link) =>
        link is ['/'] or ['/', not ('/' or '\\'), ..] && !link.Any(char.IsControl);

    private static bool IsTrue(JsonElement? element, string name) =>
        element is { } found
        && found.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.True;

    private static string? GetString(JsonElement? element, string name) =>
        element is { } found
        && found.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;

    // read as a decimal so the rule is exactly set_post_artworks's: any whole number an int can
    // hold, written as 42, 42.0 or 4.2e1
    private static int? GetInt(JsonElement? element, string name) =>
        element is { } found
        && found.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.Number
        && value.TryGetDecimal(out var number)
        && number == decimal.Truncate(number)
        && number is >= int.MinValue and <= int.MaxValue
            ? (int)number
            : null;

    // [GeneratedRegex] writes the matching code at compile time instead of building it at run time.
    // \z is the very end: $ would also match before a final "\n", letting one through
    [GeneratedRegex(@"^[0-9a-f]{32}\z")]
    private static partial Regex StorageKeyPattern();

    // what ImageProcessor makes, in no more room than artwork_images.blur_data_uri gives it
    private const int MaximumBlurLength = 1000;

    [GeneratedRegex(@"^data:image/webp;base64,[A-Za-z0-9+/]+={0,2}\z")]
    private static partial Regex BlurPattern();
}
