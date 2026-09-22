namespace ArtistShop.Web.Domain.Publishing;

using System.Text.Json;
using System.Text.RegularExpressions;

// Reads the editor's Delta into a PostDocument. A Delta is a flat list of ops, not a tree: text
// arrives in runs carrying their own bold/italic/link, and a line's heading, list or quote format
// rides on the "\n" that ends the line. Anything not recognised is dropped rather than refused,
// so a post keeps showing what it can if the editor ever stores something new
public static partial class PostDocumentParser
{
    public static PostDocument Parse(PostBody body)
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
                AddText(blocks, line, insert.GetString() ?? "", attributes);
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
        JsonElement? attributes
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
                line.Add(
                    new PostText(
                        segments[index],
                        IsTrue(attributes, "bold"),
                        IsTrue(attributes, "italic"),
                        IsTrue(attributes, "underline"),
                        SafeLink(GetString(attributes, "link"))
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
        if (insert.TryGetProperty("artwork", out var artwork) && artwork.ValueKind is JsonValueKind.Object)
        {
            if (GetInt(artwork, "artworkId") is not int artworkId)
            {
                return null;
            }

            var storageKey = GetString(artwork, "storageKey");

            return new ArtworkEmbedBlock(
                artworkId,
                // it becomes part of an image address, so it must look like one of ours
                storageKey is not null && StorageKeyPattern().IsMatch(storageKey) ? storageKey : null,
                GetString(artwork, "size") is "small" ? EmbedImageSize.Small : EmbedImageSize.Medium,
                ReadLayout(GetString(artwork, "layout"))
            );
        }

        if (insert.TryGetProperty("youtube", out var youtube) && youtube.ValueKind is JsonValueKind.Object)
        {
            var videoId = GetString(youtube, "videoId");

            return videoId is not null && YouTubeVideoIdPattern().IsMatch(videoId)
                ? new YouTubeEmbedBlock(videoId, ReadLayout(GetString(youtube, "layout")))
                : null;
        }

        return null;
    }

    private static ListStyle? ReadListStyle(string? value) =>
        value switch
        {
            "bullet" => ListStyle.Bullet,
            "ordered" => ListStyle.Ordered,
            _ => null,
        };

    private static EmbedLayout ReadLayout(string? value) =>
        value switch
        {
            "floatLeft" => EmbedLayout.FloatLeft,
            "floatRight" => EmbedLayout.FloatRight,
            _ => EmbedLayout.Center,
        };

    // Blazor encodes an href's characters but will still write javascript:alert(1) into one, so
    // the scheme is checked here
    private static string? SafeLink(string? link) =>
        Uri.TryCreate(link, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto)
            ? link
            : null;

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

    private static int? GetInt(JsonElement? element, string name) =>
        element is { } found
        && found.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.Number
        && value.TryGetInt32(out var number)
            ? number
            : null;

    // [GeneratedRegex] writes the matching code at compile time instead of building it at run time.
    // \z is the very end: $ would also match before a final "\n", letting one through
    [GeneratedRegex(@"^[0-9a-f]{32}\z")]
    private static partial Regex StorageKeyPattern();

    // every YouTube video id is eleven of these characters
    [GeneratedRegex(@"^[A-Za-z0-9_-]{11}\z")]
    private static partial Regex YouTubeVideoIdPattern();
}
