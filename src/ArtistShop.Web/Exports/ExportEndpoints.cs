namespace ArtistShop.Web.Exports;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
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

    public const string ImageExportRunningMessage = "Another image download from this website is running. Try again when it has finished.";

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
        ImageExportLock imageExportLock
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

        return await StreamImagesAsync(context, currentSite, imageStorage, imageExportLock, part.Entries, DownloadName(currentSite.MainHost.Value, part, date));
    }

    // every part in one zip, the same folders side by side
    private static async Task<IResult> DownloadAllImagesAsync(
        HttpContext context,
        CurrentSite currentSite,
        TimeProvider timeProvider,
        ArtworkRepository artworkRepository,
        ImageStorage imageStorage,
        ImageExportLock imageExportLock
    )
    {
        var entries = ImageExportPlan.Parts(await artworkRepository.GetAllAsync()).SelectMany(part => part.Entries);
        var date = DateText.FileNameDay(timeProvider.GetUtcNow());

        return await StreamImagesAsync(context, currentSite, imageStorage, imageExportLock, entries, $"{currentSite.MainHost.Value}-images-{date}");
    }

    private static async Task<IResult> StreamImagesAsync(
        HttpContext context,
        CurrentSite currentSite,
        ImageStorage imageStorage,
        ImageExportLock imageExportLock,
        IEnumerable<ImageExportEntry> entries,
        string name
    )
    {
        using var running = imageExportLock.TryAcquire(currentSite.Id);

        if (running is null)
        {
            return TypedResults.Text(ImageExportRunningMessage, statusCode: StatusCodes.Status409Conflict);
        }

        context.Response.ContentType = "application/zip";
        context.Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileNameStar = $"{name}.zip" }.ToString();
        // nginx would otherwise keep what the browser hasn't taken yet in a file on its own disk,
        // gigabytes of it for a slow download
        context.Response.Headers["X-Accel-Buffering"] = "no";

        // written inside the handler, not by a returned result, so the lock is held until the last byte
        await ImageExportArchive.WriteAsync(context.Response.Body, entries, name, imageStorage, context.RequestAborted);

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
