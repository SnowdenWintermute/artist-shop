namespace ArtistShop.Web.Images;

using ArtistShop.Web.Sites;
using Microsoft.AspNetCore.Http.HttpResults;

// Serves the current site's variants at ImageUrls.VariantsRequestPath. An endpoint rather than the
// static files middleware, which serves one folder for every request, while each site has its own:
// a site's host can only ever serve that site's files
public static class VariantEndpoints
{
    // an address never gets different bytes (each upload has a new storage key), so browsers may
    // keep a variant for a week without asking again
    private const string CacheControl = "public, max-age=604800, immutable";

    public static void MapVariantEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapMethods(
                $"{ImageUrls.VariantsRequestPath}/{{storageKey}}/{{fileName}}",
                [HttpMethods.Get, HttpMethods.Head],
                Serve
            )
            .WithMetadata(new ServedOnAttribute(HostTypes.Site));

    public static Results<PhysicalFileHttpResult, NotFound> Serve(
        string storageKey,
        string fileName,
        ImageStorage imageStorage,
        HttpResponse response
    )
    {
        // only names ImageProcessor writes, so no route value can reach outside the variants folder
        if (!Guid.TryParseExact(storageKey, "N", out _) || ImageVariants.ReadFileName(fileName) is not { } format)
        {
            return TypedResults.NotFound();
        }

        var file = new FileInfo(Path.Combine(imageStorage.VariantDirectory(storageKey), fileName));

        if (!file.Exists)
        {
            return TypedResults.NotFound();
        }

        response.Headers.CacheControl = CacheControl;

        // the modified time answers a browser's If-Modified-Since with 304
        return TypedResults.PhysicalFile(
            file.FullName,
            ImageVariants.ContentType(format),
            lastModified: file.LastWriteTimeUtc
        );
    }
}
