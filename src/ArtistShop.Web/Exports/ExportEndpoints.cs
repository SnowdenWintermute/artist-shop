namespace ArtistShop.Web.Exports;

using System.Globalization;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Imports;
using ArtistShop.Web.Sites;
using Microsoft.AspNetCore.Http.HttpResults;

// the downloads on the Export page
public static class ExportEndpoints
{
    public const string CatalogPath = "/admin/export/catalog";

    public static void MapExportEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(CatalogPath, DownloadCatalogAsync)
            .RequireAuthorization(SitePolicies.Admin)
            .WithMetadata(new ServedOnAttribute(HostTypes.Site));

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
        var date = timeProvider.GetUtcNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var name = $"{currentSite.MainHost.Value}-catalog-{date}";
        var archive = CatalogExportArchive.Create(snapshot, await artworkRepository.GetAllAsync(), name);

        return TypedResults.File(archive, "application/zip", $"{name}.zip");
    }
}
