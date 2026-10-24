using System.Text.Json.Nodes;

namespace ArtistShop.Web.Domain.Publishing;

// Writes a RichTextDocument back into the editor's Delta, the reverse of RichTextParser: parsing
// what this writes gives the same document back. The post import stores what this writes rather
// than the file it read, so the editor only ever loads a Delta made here
public static class RichTextDeltaWriter
{
    public static RichText Write(RichTextDocument document) => new(Delta(document, Embed).ToJsonString());

    // stored text as this writes it, so the editor never loads a format only older text has, such as
    // a heading's level
    public static RichText Rewrite(RichText body) => Write(RichTextParser.Parse(body));

    // Embed gives each embed block's insert, such as {"artshop-image": {...}}, or null to leave the
    // embed out
    public static JsonObject Delta(RichTextDocument document, Func<RichTextBlock, JsonObject?> embed)
    {
        var ops = new JsonArray();
        var endsWithLine = false;

        foreach (var block in document.Blocks)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                    AddLine(ops, paragraph.Text, new JsonObject(), paragraph.Alignment);
                    endsWithLine = true;
                    break;

                case HeadingBlock heading:
                    AddLine(ops, heading.Text, new JsonObject { [RichTextParser.HeadingName] = true }, heading.Alignment);
                    endsWithLine = true;
                    break;

                case BlockquoteBlock quote:
                    AddLine(ops, quote.Text, new JsonObject { ["blockquote"] = true }, quote.Alignment);
                    endsWithLine = true;
                    break;

                case ListBlock list:
                    foreach (var item in list.Items)
                    {
                        AddLine(
                            ops,
                            item,
                            new JsonObject { ["list"] = list.Style is ListStyle.Bullet ? "bullet" : "ordered" },
                            list.Alignment
                        );
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

        // Quill's document always ends with a line, as RichText.Empty does
        if (!endsWithLine)
        {
            ops.Add(new JsonObject { ["insert"] = "\n" });
        }

        return new JsonObject { ["ops"] = ops };
    }

    // each embed as the editor stores it
    public static JsonObject? Embed(RichTextBlock block) =>
        block switch
        {
            WorkEmbedBlock work => new JsonObject
            {
                [RichTextParser.WorkEmbedName] = WithLook(
                    new JsonObject { ["workId"] = work.WorkId.Value, ["storageKey"] = work.StorageKey },
                    work.Size,
                    work.Layout,
                    work.Caption
                ),
            },
            UploadedImageEmbedBlock image => new JsonObject
            {
                [RichTextParser.UploadedImageEmbedName] = ImageValue(new JsonObject { ["storageKey"] = image.StorageKey }, image),
            },
            VideoEmbedBlock video => new JsonObject { [RichTextParser.VideoEmbedName] = VideoValue(video) },
            _ => null,
        };

    // An uploaded image's own settings added to where its file is: a storage key here, a file in the
    // folder for the export
    public static JsonObject ImageValue(JsonObject where, UploadedImageEmbedBlock image)
    {
        WithLook(where, image.Size, image.Layout, image.Caption);

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
            _ => throw new InvalidOperationException($"{video.Source.GetType().Name} isn't a video source."),
        };

        value["layout"] = EmbedLayoutNames.Of(video.Layout);

        return value;
    }

    // a run's formats ride on the run, and the line's own, with its alignment, ride on the "\n" that ends it
    private static void AddLine(JsonArray ops, IReadOnlyList<RichTextRun> text, JsonObject lineFormat, TextAlignment alignment)
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

            if (run.Size is not TextSize.Size16)
            {
                formats["size"] = TextSizeNames.Of(run.Size);
            }

            ops.Add(formats.Count is 0 ? new JsonObject { ["insert"] = run.Text } : new JsonObject { ["insert"] = run.Text, ["attributes"] = formats });
        }

        // Quill leaves a left-aligned line without the format
        if (alignment is not TextAlignment.Left)
        {
            lineFormat["align"] =
                alignment is TextAlignment.Center ? RichTextParser.AlignCenterValue : RichTextParser.AlignRightValue;
        }

        ops.Add(lineFormat.Count is 0 ? new JsonObject { ["insert"] = "\n" } : new JsonObject { ["insert"] = "\n", ["attributes"] = lineFormat });
    }
}
