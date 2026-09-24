namespace ArtistShop.Web.Images;

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;

// Serves the current site's variants at ImageUrls.VariantsRequestPath. An endpoint rather than the
// static files middleware, which serves one folder for every request, while each site has its own:
// a site's host can only ever serve that site's files
public static partial class VariantEndpoints
{
    // an address never gets different bytes (each upload has a new storage key), so browsers may
    // keep a variant for a week without asking again
    private const string CacheControl = "public, max-age=604800, immutable";

    public static void MapVariantEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapMethods(
            $"{ImageUrls.VariantsRequestPath}/{{storageKey}}/{{fileName}}",
            [HttpMethods.Get, HttpMethods.Head],
            Serve
        );

    public static Results<PhysicalFileHttpResult, NotFound> Serve(
        string storageKey,
        string fileName,
        ImageStorage imageStorage,
        HttpResponse response
    )
    {
        // only names ImageProcessor writes, so no route value can reach outside the variants folder
        if (!Guid.TryParseExact(storageKey, "N", out _) || ContentTypeOf(fileName) is not { } contentType)
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
        return TypedResults.PhysicalFile(file.FullName, contentType, lastModified: file.LastWriteTimeUtc);
    }

    private static string? ContentTypeOf(string fileName) =>
        VariantFileNamePattern().Match(fileName) is { Success: true } match
            ? match.Groups["extension"].Value switch
            {
                "avif" => "image/avif",
                "webp" => "image/webp",
                _ => null,
            }
            : null;

    // ImageVariants.FileName's names, such as 800.avif; \z rather than $, which also allows a
    // trailing line break
    [GeneratedRegex(@"\A[0-9]+\.(?<extension>avif|webp)\z")]
    private static partial Regex VariantFileNamePattern();
}
