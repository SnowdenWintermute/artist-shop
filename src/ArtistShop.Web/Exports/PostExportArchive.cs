using System.IO.Compression;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Images;
using ArtistShop.Web.Publishing;

namespace ArtistShop.Web.Exports;

// A post and its parsed body
public record ExportedPost(Post Post, RichTextDocument Document);

// The post download: a README, an index page, then a folder for each post holding its page, its
// images and the post.json the post import reads
public static class PostExportArchive
{
    // A work's picture is a web copy about this wide: sharp at twice the medium embed, the
    // widest a post shows it
    public const int WebCopyWidth = 800;

    // heads the README's list of posts with smaller copies
    public const string SmallerCopiesHeading = "Smaller images";

    // drafts included, with every work image the posts embed
    public static async Task<(List<ExportedPost> Posts, IReadOnlyDictionary<string, WorkImageWithWork> WorkImages)> LoadPostsAsync(
        PostRepository postRepository,
        WorkRepository workRepository,
        ImageStorage imageStorage
    )
    {
        // missing uploads left out, as on the post page
        List<ExportedPost> posts =
        [
            .. (await postRepository.GetAllWithBodiesAsync()).Select(post => new ExportedPost(
                post,
                LoadedRichText.Parse(post.Body, imageStorage)
            )),
        ];

        var workImages = await LoadedRichText.GetWorkImagesAsync(posts.Select(post => post.Document), workRepository);

        return (posts, workImages);
    }

    // the posts a download made now would give a smaller copy of an image, since the image's web copy
    // size isn't made yet. The Export page warns before, and the README after
    public static List<Post> PostsWithSmallerCopies(
        IEnumerable<ExportedPost> posts,
        IReadOnlyDictionary<string, WorkImageWithWork> workImages,
        ImageSizeQueue imageSizeQueue
    ) =>
        [
            .. posts
                .Where(exported =>
                    exported.Document.Blocks.Any(block =>
                        ImageOf(block, workImages) is { } source && imageSizeQueue.IsProcessing(source.StorageKey, WebCopyWidth)
                    )
                )
                .Select(exported => exported.Post),
        ];

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
        ImageSizeQueue imageSizeQueue,
        CancellationToken cancellationToken
    )
    {
        await using var zip = await ExportZip.CreateAsync(destination, cancellationToken);
        await AddAsync(zip, posts, workImages, folderName, host, siteOrigin, imageStorage, imageSizeQueue, cancellationToken);
    }

    public static async Task AddAsync(
        ZipArchive zip,
        IReadOnlyList<ExportedPost> posts,
        IReadOnlyDictionary<string, WorkImageWithWork> workImages,
        string folderName,
        string host,
        string siteOrigin,
        ImageStorage imageStorage,
        ImageSizeQueue imageSizeQueue,
        CancellationToken cancellationToken
    )
    {
        var readme = Readme(host) + SmallerCopiesNote(PostsWithSmallerCopies(posts, workImages, imageSizeQueue));
        await ExportZip.AddTextAsync(zip, $"{folderName}/{ExportZip.ReadmeFileName}", readme, cancellationToken);
        await ExportZip.AddTextAsync(zip, $"{folderName}/{PostExportHtml.IndexFileName}", PostExportHtml.Index(host, [.. posts.Select(exported => exported.Post)]), cancellationToken);

        foreach (var (post, document) in posts)
        {
            var postFolder = $"{folderName}/{post.Slug.Value}";
            var images = new Dictionary<RichTextBlock, PostExportImage>();
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

                // the widest made so far, when the web copy size isn't yet; the README says which posts
                var webCopyWidth = ImageVariants.LargestWidthUpTo(imageSizeQueue.ReadyWidths(source.StorageKey, source.Width), WebCopyWidth);
                var webCopy = imageStorage.OpenVariant(source.StorageKey, ImageVariants.FileName(webCopyWidth, ImageVariantFormat.Webp));

                // a web copy or original gone since the post was saved leaves the embed out
                if (webCopy is null || !imageStorage.OriginalExists(source.StorageKey))
                {
                    webCopy?.Content.Dispose();
                    continue;
                }

                var name = $"image-{filesByStorageKey.Count + 1}";
                var fileName = name + ExportImageFormat.Webp.Extension;

                await using (webCopy.Content)
                {
                    await ExportZip.AddFileAsync(zip, $"{postFolder}/{fileName}", webCopy.Content, ExportZip.CompressionFor(ExportImageFormat.Webp), cancellationToken);
                }

                var originalFileName = source.KeepsOriginal
                    ? await AddOriginalAsync(zip, postFolder, name, imageStorage, source.StorageKey, cancellationToken)
                    : null;

                // a work's original is in the image download, not here, so post.json names it by its hash
                if (!source.KeepsOriginal)
                {
                    sha256ByStorageKey[source.StorageKey] = await imageStorage.OriginalSha256Async(source.StorageKey, cancellationToken);
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
        ImageStorage imageStorage,
        string storageKey,
        CancellationToken cancellationToken
    )
    {
        // checked by the caller just before
        await using var original = imageStorage.OpenOriginal(storageKey) ?? throw new FileNotFoundException("The original is gone.", storageKey);
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

    private static string SmallerCopiesNote(List<Post> posts) =>
        posts.Count is 0
            ? ""
            : $"""


              {SmallerCopiesHeading}
                These posts' newest images were still being processed, so their copies here are smaller.
                Download again in a few minutes for the full ones.
              {string.Join("\n", posts.Select(post => $"  - {post.Slug.Value}/"))}
              """;

    // KeepsOriginal is true for an image uploaded into the post
    private record ImageSource(string StorageKey, int Width, int Height, string Alt, bool KeepsOriginal);

    // null for a block that isn't an image, and for a work embed the post page leaves out
    private static ImageSource? ImageOf(RichTextBlock block, IReadOnlyDictionary<string, WorkImageWithWork> workImages) =>
        block switch
        {
            WorkEmbedBlock embed when WorkEmbeds.SourceOf(embed, workImages) is { } source =>
                new ImageSource(source.Image.StorageKey, source.Image.Width, source.Image.Height, source.Work.Name.Value, KeepsOriginal: false),
            UploadedImageEmbedBlock embed => new ImageSource(embed.StorageKey, embed.Width, embed.Height, embed.Alt, KeepsOriginal: true),
            _ => null,
        };
}
