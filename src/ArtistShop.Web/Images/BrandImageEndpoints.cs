using System.Text.RegularExpressions;
using ArtistShop.Web.Security;
using ArtistShop.Web.Sites;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ArtistShop.Web.Images;

// Serves the current site's logos and favicons at BrandImageUrls.RequestPath, from its database
public static partial class BrandImageEndpoints
{
    public static void MapBrandImageEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapMethods($"{BrandImageUrls.RequestPath}/{{sha256}}.{{extension}}", [HttpMethods.Get, HttpMethods.Head], ServeAsync)
            .WithMetadata(new ServedOnAttribute(HostTypes.Site), new SandboxedResponseAttribute());

    public static async Task<Results<FileContentHttpResult, NotFound>> ServeAsync(
        string sha256,
        string extension,
        BrandImageStore store,
        HttpResponse response
    )
    {
        if (!Sha256Pattern().IsMatch(sha256) || await store.GetFileAsync(sha256) is not { } file)
        {
            return TypedResults.NotFound();
        }

        // one address per file, so another extension on the same hash isn't one
        if (BrandImageFormats.Extension(file.Format) != extension)
        {
            return TypedResults.NotFound();
        }

        // the address holds the file's hash, so it never gets different bytes
        response.Headers.CacheControl = ImageUrls.CacheControl;
        // the browser takes the content type as given, so a file is never read as anything else
        response.Headers.XContentTypeOptions = "nosniff";

        return TypedResults.File(file.Bytes, BrandImageFormats.ContentType(file.Format));
    }

    // \z rather than $, which also allows a trailing line break
    [GeneratedRegex(@"\A[0-9a-f]{64}\z")]
    private static partial Regex Sha256Pattern();
}
