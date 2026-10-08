using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Exports;

// The post.json beside each exported post's page, which the post import reads back: the post's
// Delta with this website's ids swapped for what another website can find. An uploaded image names
// its file in the folder. A work embed names its work's slug and title, and the SHA-256 of
// its image's original, which is the same file on any website the image download went to
public static class PostExportJson
{
    public const string FileName = "post.json";

    // the version of this file's layout, so a later import can tell an older file apart
    public const int FormatVersion = 1;

    public const string FormatVersionProperty = "formatVersion";
    public const string TitleProperty = "title";
    public const string PublishedAtProperty = "publishedAt";
    public const string BodyProperty = "body";

    // in an embed's value
    public const string FileProperty = "file";
    public const string WorkSlugProperty = "workSlug";
    public const string WorkTitleProperty = "workTitle";
    public const string ImageSha256Property = "imageSha256";

    // indented, and with accented letters as they are rather than as \u escapes, for a person reading it
    public static readonly JsonSerializerOptions Readable = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    // Images is the file for each image embed there is one for, as for the page; an embed missing
    // from it is left out. Sha256ByStorageKey holds each work embed's original's hash
    public static string Write(
        Post post,
        PostDocument document,
        IReadOnlyDictionary<PostBlock, PostExportImage> images,
        IReadOnlyDictionary<string, WorkImageWithWork> workImages,
        IReadOnlyDictionary<string, string> sha256ByStorageKey
    )
    {
        var body = PostDeltaWriter.Delta(
            document,
            block =>
                block switch
                {
                    WorkEmbedBlock embed
                        when images.TryGetValue(embed, out var image)
                            && WorkEmbeds.SourceOf(embed, workImages) is { } source => new JsonObject
                        {
                            [PostDocumentParser.WorkEmbedName] = PostDeltaWriter.WithLook(
                                new JsonObject
                                {
                                    [WorkSlugProperty] = source.Work.Slug.Value,
                                    [WorkTitleProperty] = source.Work.Name.Value,
                                    [ImageSha256Property] = sha256ByStorageKey[embed.StorageKey],
                                    [FileProperty] = image.FileName,
                                },
                                embed.Size,
                                embed.Layout,
                                embed.Caption
                            ),
                        },
                    PostImageEmbedBlock embed when images.TryGetValue(embed, out var image) => new JsonObject
                    {
                        [PostDocumentParser.PostImageEmbedName] = PostDeltaWriter.ImageValue(
                            new JsonObject
                            {
                                [FileProperty] = image.OriginalFileName,
                                ["width"] = embed.Width,
                                ["height"] = embed.Height,
                            },
                            embed,
                            // processing the upload makes a new one
                            blur: null
                        ),
                    },
                    WorkEmbedBlock or PostImageEmbedBlock => null,
                    _ => PostDeltaWriter.Embed(block),
                }
        );

        var file = new JsonObject
        {
            [FormatVersionProperty] = FormatVersion,
            [TitleProperty] = post.Title.Value,
            [PublishedAtProperty] = post.PublishedAt is { } publishedAt ? DateText.DateTimeAttribute(publishedAt) : null,
            [BodyProperty] = body,
        };

        return file.ToJsonString(Readable);
    }
}
