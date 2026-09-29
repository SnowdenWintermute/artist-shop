namespace ArtistShop.Web.Exports;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Images;
using ArtistShop.Web.Imports;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Utilities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;

// the downloads on the Export page
public static class ExportEndpoints
{
    public const string CatalogPath = "/admin/export/catalog";
    public const string ImagesPath = "/admin/export/images";
    public const string AllImagesPath = "/admin/export/images/all";
    public const string PostsPath = "/admin/export/posts";

    public const string ExportRunningMessage = "Another download from this website is running. Try again when it has finished.";

    public static string ImagesUrl(ImageExportPart part) =>
        part.Series is null ? $"{ImagesPath}?type={part.Type.Id.Value}" : $"{ImagesPath}?type={part.Type.Id.Value}&series={part.Series.Id.Value}";

    public static void MapExportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // no prefix: the paths above are whole, for the page's links
        var downloads = endpoints
            .MapGroup("")
            .RequireAuthorization(SitePolicies.Admin)
            .WithMetadata(new ServedOnAttribute(HostTypes.Site));

        downloads.MapGet(CatalogPath, DownloadCatalogAsync);
        downloads.MapGet(ImagesPath, DownloadImagesAsync);
        downloads.MapGet(AllImagesPath, DownloadAllImagesAsync);
        downloads.MapGet(PostsPath, DownloadPostsAsync);
    }

    private static async Task<FileContentHttpResult> DownloadCatalogAsync(
        CurrentSite currentSite,
        TimeProvider timeProvider,
        ArtworkRepository artworkRepository,
        ArtworkFieldRepository artworkFieldRepository,
        ArtworkTypeRepository artworkTypeRepository,
        VocabularyRepository vocabularyRepository
    )
    {
        var snapshot = await CatalogSetupSnapshot.LoadAsync(artworkFieldRepository, artworkTypeRepository, vocabularyRepository);
        var date = DateText.FileNameDay(timeProvider.GetUtcNow());
        var name = $"{currentSite.MainHost.Value}-catalog-{date}";
        var archive = CatalogExportArchive.Create(snapshot, await artworkRepository.GetAllAsync(), name);

        return TypedResults.File(archive, "application/zip", $"{name}.zip");
    }

    // the series is left out for the type's artworks in no series
    private static async Task<IResult> DownloadImagesAsync(
        int type,
        int? series,
        HttpContext context,
        CurrentSite currentSite,
        TimeProvider timeProvider,
        ArtworkRepository artworkRepository,
        ImageStorage imageStorage,
        ExportLock exportLock
    )
    {
        var part = ImageExportPlan
            .Parts(await artworkRepository.GetAllAsync())
            .SingleOrDefault(part => part.Type.Id == new ArtworkTypeId(type) && part.Series?.Id.Value == series);

        if (part is null)
        {
            return TypedResults.NotFound();
        }

        var date = DateText.FileNameDay(timeProvider.GetUtcNow());

        var name = DownloadName(currentSite.MainHost.Value, part, date);

        return await StreamZipAsync(
            context,
            currentSite,
            exportLock,
            name,
            (body, cancellationToken) => ImageExportArchive.WriteAsync(body, part.Entries, name, imageStorage, cancellationToken)
        );
    }

    // every part in one zip, the same folders side by side
    private static async Task<IResult> DownloadAllImagesAsync(
        HttpContext context,
        CurrentSite currentSite,
        TimeProvider timeProvider,
        ArtworkRepository artworkRepository,
        ImageStorage imageStorage,
        ExportLock exportLock
    )
    {
        var entries = ImageExportPlan.Parts(await artworkRepository.GetAllAsync()).SelectMany(part => part.Entries);
        var date = DateText.FileNameDay(timeProvider.GetUtcNow());

        var name = $"{currentSite.MainHost.Value}-images-{date}";

        return await StreamZipAsync(
            context,
            currentSite,
            exportLock,
            name,
            (body, cancellationToken) => ImageExportArchive.WriteAsync(body, entries, name, imageStorage, cancellationToken)
        );
    }

    // drafts included. A link to a page on the site gets the address the download came from
    private static async Task<IResult> DownloadPostsAsync(
        HttpContext context,
        CurrentSite currentSite,
        TimeProvider timeProvider,
        PostRepository postRepository,
        ArtworkRepository artworkRepository,
        ImageStorage imageStorage,
        ExportLock exportLock
    )
    {
        // missing uploads left out, as on the post page
        List<ExportedPost> posts =
        [
            .. (await postRepository.GetAllWithBodiesAsync()).Select(post => new ExportedPost(
                post,
                PostImageFiles.WithoutMissing(PostDocumentParser.Parse(post.Body), imageStorage)
            )),
        ];

        // every artwork embed's image in one query, however many posts there are
        var artworkImages = await artworkRepository.GetImagesByStorageKeyAsync(
            [
                .. posts
                    .SelectMany(post => post.Document.Blocks.OfType<ArtworkEmbedBlock>())
                    .Select(embed => embed.StorageKey)
                    .Distinct(),
            ]
        );
        var host = currentSite.MainHost.Value;
        var name = $"{host}-posts-{DateText.FileNameDay(timeProvider.GetUtcNow())}";
        var siteOrigin = $"{context.Request.Scheme}://{context.Request.Host}";

        return await StreamZipAsync(
            context,
            currentSite,
            exportLock,
            name,
            (body, cancellationToken) =>
                PostExportArchive.WriteAsync(body, posts, artworkImages, name, host, siteOrigin, imageStorage, cancellationToken)
        );
    }

    private static async Task<IResult> StreamZipAsync(
        HttpContext context,
        CurrentSite currentSite,
        ExportLock exportLock,
        string name,
        Func<Stream, CancellationToken, Task> writeAsync
    )
    {
        using var running = exportLock.TryAcquire(currentSite.Id);

        if (running is null)
        {
            return TypedResults.Text(ExportRunningMessage, statusCode: StatusCodes.Status409Conflict);
        }

        context.Response.ContentType = "application/zip";
        context.Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileNameStar = $"{name}.zip" }.ToString();
        // nginx would otherwise keep what the browser hasn't taken yet in a file on its own disk,
        // gigabytes of it for a slow download
        context.Response.Headers["X-Accel-Buffering"] = "no";

        // written inside the handler, not by a returned result, so the lock is held until the last byte
        await writeAsync(context.Response.Body, context.RequestAborted);

        return TypedResults.Empty;
    }

    // named after the folders, or their ids when that name is too long for a file
    private static string DownloadName(string host, ImageExportPart part, string date)
    {
        var folders = part.SeriesFolder is null ? part.TypeFolder : $"{part.TypeFolder}-{part.SeriesFolder}";
        var ids = part.Series is null ? $"{part.Type.Id.Value}" : $"{part.Type.Id.Value}-{part.Series.Id.Value}";
        var name = $"{host}-images-{folders}-{date}";

        return ExportFileNames.IsPortable($"{name}.zip") ? name : $"{host}-images-{ids}-{date}";
    }
}
