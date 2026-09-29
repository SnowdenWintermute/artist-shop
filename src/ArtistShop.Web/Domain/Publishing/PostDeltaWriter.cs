using System.Text.Json.Nodes;

namespace ArtistShop.Web.Domain.Publishing;

// Writes a PostDocument back into the editor's Delta, the reverse of PostDocumentParser: parsing
// what this writes gives the same document back. The post import stores what this writes rather
// than the file it read, so the editor only ever loads a Delta made here
public static class PostDeltaWriter
{
    public static PostBody Write(PostDocument document) => new(Delta(document, Embed).ToJsonString());

    // Embed gives each embed block's insert, such as {"artshop-image": {...}}, or null to leave the
    // embed out
    public static JsonObject Delta(PostDocument document, Func<PostBlock, JsonObject?> embed)
    {
        var ops = new JsonArray();
        var endsWithLine = false;

        foreach (var block in document.Blocks)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                    AddLine(ops, paragraph.Text, lineFormat: null);
                    endsWithLine = true;
                    break;

                case HeadingBlock heading:
                    AddLine(ops, heading.Text, new JsonObject { ["header"] = heading.Level is HeadingLevel.Two ? 2 : 3 });
                    endsWithLine = true;
                    break;

                case BlockquoteBlock quote:
                    AddLine(ops, quote.Text, new JsonObject { ["blockquote"] = true });
                    endsWithLine = true;
                    break;

                case ListBlock list:
                    foreach (var item in list.Items)
                    {
                        AddLine(ops, item, new JsonObject { ["list"] = list.Style is ListStyle.Bullet ? "bullet" : "ordered" });
                    }

                    endsWithLine = true;
                    break;

                default:
                    if (embed(block) is { } insert)
                    {
                        ops.Add(new JsonObject { ["insert"] = insert });
                        endsWithLine = false;
                    }

                    break;
            }
        }

        // Quill's document always ends with a line, as PostBody.Empty does
        if (!endsWithLine)
        {
            ops.Add(new JsonObject { ["insert"] = "\n" });
        }

        return new JsonObject { ["ops"] = ops };
    }

    // each embed as the editor stores it
    public static JsonObject? Embed(PostBlock block) =>
        block switch
        {
            ArtworkEmbedBlock artwork => new JsonObject
            {
                [PostDocumentParser.ArtworkEmbedName] = WithLook(
                    new JsonObject { ["artworkId"] = artwork.ArtworkId.Value, ["storageKey"] = artwork.StorageKey },
                    artwork.Size,
                    artwork.Layout,
                    artwork.Caption
                ),
            },
            PostImageEmbedBlock image => new JsonObject
            {
                [PostDocumentParser.PostImageEmbedName] = ImageValue(
                    new JsonObject
                    {
                        ["storageKey"] = image.StorageKey,
                        ["width"] = image.Width,
                        ["height"] = image.Height,
                    },
                    image,
                    image.BlurDataUri
                ),
            },
            VideoEmbedBlock video => new JsonObject { [PostDocumentParser.VideoEmbedName] = VideoValue(video) },
            _ => null,
        };

    // An uploaded image's own settings added to where its file is: a storage key here, a file in the
    // folder for the export. Blur is null to leave it out
    public static JsonObject ImageValue(JsonObject where, PostImageEmbedBlock image, string? blur)
    {
        if (blur is not null)
        {
            where["blur"] = blur;
        }

        WithLook(where, image.Size, image.Layout, image.Caption);
        where["alt"] = image.Alt;

        if (image.OpensLightbox)
        {
            where["lightbox"] = true;
        }

        return where;
    }

    // what every image embed has, added to the value
    public static JsonObject WithLook(JsonObject value, EmbedImageSize size, EmbedLayout layout, string? caption)
    {
        value["size"] = EmbedImageSizeNames.Of(size);
        value["layout"] = EmbedLayoutNames.Of(layout);

        if (caption is not null)
        {
            value["caption"] = caption;
        }

        return value;
    }

    private static JsonObject VideoValue(VideoEmbedBlock video)
    {
        var value = video.Source switch
        {
            YouTubeVideo youTube => new JsonObject { ["provider"] = VideoSources.YouTubeProvider, ["videoId"] = youTube.Id },
            VimeoVideo vimeo when vimeo.UnlistedHash is { } hash => new JsonObject
            {
                ["provider"] = VideoSources.VimeoProvider,
                ["videoId"] = vimeo.Id,
                ["hash"] = hash,
            },
            VimeoVideo vimeo => new JsonObject { ["provider"] = VideoSources.VimeoProvider, ["videoId"] = vimeo.Id },
            _ => throw new ArgumentOutOfRangeException(nameof(video), video.Source, null),
        };

        value["layout"] = EmbedLayoutNames.Of(video.Layout);

        return value;
    }

    // a run's formats ride on the run, and the line's own ride on the "\n" that ends it
    private static void AddLine(JsonArray ops, IReadOnlyList<PostText> text, JsonObject? lineFormat)
    {
        foreach (var run in text)
        {
            var formats = new JsonObject();

            if (run.Bold)
            {
                formats["bold"] = true;
            }

            if (run.Italic)
            {
                formats["italic"] = true;
            }

            if (run.Underline)
            {
                formats["underline"] = true;
            }

            if (run.Link is { } link)
            {
                formats["link"] = link;
            }

            ops.Add(formats.Count is 0 ? new JsonObject { ["insert"] = run.Text } : new JsonObject { ["insert"] = run.Text, ["attributes"] = formats });
        }

        ops.Add(lineFormat is null ? new JsonObject { ["insert"] = "\n" } : new JsonObject { ["insert"] = "\n", ["attributes"] = lineFormat });
    }
}
