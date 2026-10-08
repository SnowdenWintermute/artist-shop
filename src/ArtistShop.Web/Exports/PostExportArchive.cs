using System.IO.Compression;
using System.Security.Cryptography;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Images;

namespace ArtistShop.Web.Exports;

// A post and its parsed body
public record ExportedPost(Post Post, PostDocument Document);

// The post download: a README, an index page, then a folder for each post holding its page, its
// images and the post.json the post import reads
public static class PostExportArchive
{
    // A work's picture is a web copy about this wide: sharp at twice the medium embed, the
    // widest a post shows it
    public const int WebCopyWidth = 800;

    // A post's image files, in the order the post shows them, named image-1, image-2 and so on.
    // An image the post shows twice is one file. WorkImages holds every work embed's image by
    // storage key, as the post page loads them
    public static async Task WriteAsync(
        Stream destination,
        IReadOnlyList<ExportedPost> posts,
        IReadOnlyDictionary<string, WorkImageWithWork> workImages,
        string folderName,
        string host,
        string siteOrigin,
        ImageStorage imageStorage,
        CancellationToken cancellationToken
    )
    {
        await using var zip = await ExportZip.CreateAsync(destination, cancellationToken);
        await AddAsync(zip, posts, workImages, folderName, host, siteOrigin, imageStorage, cancellationToken);
    }

    public static async Task AddAsync(
        ZipArchive zip,
        IReadOnlyList<ExportedPost> posts,
        IReadOnlyDictionary<string, WorkImageWithWork> workImages,
        string folderName,
        string host,
        string siteOrigin,
        ImageStorage imageStorage,
        CancellationToken cancellationToken
    )
    {
        await ExportZip.AddTextAsync(zip, $"{folderName}/{ExportZip.ReadmeFileName}", Readme(host), cancellationToken);
        await ExportZip.AddTextAsync(zip, $"{folderName}/{PostExportHtml.IndexFileName}", PostExportHtml.Index(host, [.. posts.Select(exported => exported.Post)]), cancellationToken);

        foreach (var (post, document) in posts)
        {
            var postFolder = $"{folderName}/{post.Slug.Value}";
            var images = new Dictionary<PostBlock, PostExportImage>();
            var filesByStorageKey = new Dictionary<string, PostExportImage>();
            var sha256ByStorageKey = new Dictionary<string, string>();

            foreach (var block in document.Blocks)
            {
                if (ImageOf(block, workImages) is not { } source)
                {
                    continue;
                }

                if (filesByStorageKey.TryGetValue(source.StorageKey, out var existing))
                {
                    images[block] = existing with { Alt = source.Alt };
                    continue;
                }

                var webCopyPath = Path.Combine(
                    imageStorage.VariantDirectory(source.StorageKey),
                    ImageVariants.FileName(ImageVariants.LargestWidthUpTo(source.Width, WebCopyWidth), ImageVariantFormat.Webp)
                );

                // a web copy or original gone since the post was saved leaves the embed out
                if (!File.Exists(webCopyPath) || !imageStorage.OriginalExists(source.StorageKey))
                {
                    continue;
                }

                var name = $"image-{filesByStorageKey.Count + 1}";
                var fileName = name + ExportImageFormat.Webp.Extension;

                await using (var webCopy = ExportZip.OpenFile(webCopyPath))
                {
                    await ExportZip.AddFileAsync(zip, $"{postFolder}/{fileName}", webCopy, ExportZip.CompressionFor(ExportImageFormat.Webp), cancellationToken);
                }

                var originalFileName = source.KeepsOriginal
                    ? await AddOriginalAsync(zip, postFolder, name, imageStorage.OriginalPath(source.StorageKey), cancellationToken)
                    : null;

                // a work's original is in the image download, not here, so post.json names it by its hash
                if (!source.KeepsOriginal)
                {
                    sha256ByStorageKey[source.StorageKey] = await Sha256Async(imageStorage.OriginalPath(source.StorageKey), cancellationToken);
                }

                var image = new PostExportImage(fileName, originalFileName, source.Alt, source.Width, source.Height);
                filesByStorageKey[source.StorageKey] = image;
                images[block] = image;
            }

            await ExportZip.AddTextAsync(
                zip,
                $"{folderName}/{PostExportHtml.PagePath(post)}",
                PostExportHtml.Page(post, document, images, siteOrigin),
                cancellationToken
            );

            await ExportZip.AddTextAsync(
                zip,
                $"{postFolder}/{PostExportJson.FileName}",
                PostExportJson.Write(post, document, images, workImages, sha256ByStorageKey),
                cancellationToken
            );
        }
    }

    // The upload itself, since it's in no other download: image-1-original.tiff beside image-1.webp
    private static async Task<string> AddOriginalAsync(
        ZipArchive zip,
        string postFolder,
        string name,
        string originalPath,
        CancellationToken cancellationToken
    )
    {
        await using var original = ExportZip.OpenFile(originalPath);
        var format = await ExportImageFormat.ReadAsync(original, cancellationToken);
        var fileName = $"{name}-original{format?.Extension ?? ""}";

        await ExportZip.AddFileAsync(zip, $"{postFolder}/{fileName}", original, ExportZip.CompressionFor(format), cancellationToken);

        return fileName;
    }

    private static string Readme(string host) =>
        $"""
        Posts from {host}

        {PostExportHtml.IndexFileName}
          Open this in a web browser. It lists every post, published ones newest first, then drafts,
          and links to each one.

        A folder for each post
          Named after the post's web address name (slug), holding the post as a web page of the same
          name and its images beside it: image-1.webp, image-2.webp and so on, in the order the post
          shows them. The pages have no scripts and nothing from the website, so they open from this
          folder and can be copied into another blog.
          - An image uploaded into the post also has its original, such as image-2-original.jpg, which
            clicking the image opens. It isn't in any other download.
          - A work's picture is a smaller copy, up to {WebCopyWidth} pixels wide. Its original is
            in the image download on the Export page.
          - Videos are links to where they're hosted.
          - Links to pages of the website point to the website.
          - Dates are the day in UTC, so one written late in the evening may show the next day.

        {PostExportJson.FileName}
          In each post's folder, the post as the post import reads it. Don't change it by hand.

        Importing into another website here
          Import the catalog and images first, so the posts' work pictures link to their works
          again. Then choose this whole folder, unzipped, under Import > Posts. A post whose title is
          already there is skipped, so running it again only adds what's missing.
        """;

    // lower case hex, as Convert.ToHexStringLower writes it
    public static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using var file = ExportZip.OpenFile(path);

        return Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellationToken));
    }

    // KeepsOriginal is true for an image uploaded into the post
    private record ImageSource(string StorageKey, int Width, int Height, string Alt, bool KeepsOriginal);

    // null for a block that isn't an image, and for a work embed the post page leaves out
    private static ImageSource? ImageOf(PostBlock block, IReadOnlyDictionary<string, WorkImageWithWork> workImages) =>
        block switch
        {
            WorkEmbedBlock embed when WorkEmbeds.SourceOf(embed, workImages) is { } source =>
                new ImageSource(source.Image.StorageKey, source.Image.Width, source.Image.Height, source.Work.Name.Value, KeepsOriginal: false),
            PostImageEmbedBlock embed => new ImageSource(embed.StorageKey, embed.Width, embed.Height, embed.Alt, KeepsOriginal: true),
            _ => null,
        };
}
