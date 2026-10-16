namespace ArtistShop.Web.Images;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Sites;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

// The text editor's image toolbar saves a library image's alt text here as the field is left, so
// every text showing the image has it. The form fields make it check the antiforgery token
public static class UploadedImageEndpoints
{
    public const string AltTextPath = "/admin/uploaded-images/alt-text";

    // the form field names the script sends
    public const string StorageKeyField = "storageKey";
    public const string AltTextField = "altText";

    public static void MapUploadedImageEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(AltTextPath, SetAltTextAsync)
            .RequireAuthorization(SitePolicies.Admin)
            .WithMetadata(new ServedOnAttribute(HostTypes.Site));

    // a 404 when the image was deleted from the library after the page was sent
    private static async Task<Results<NoContent, NotFound, BadRequest>> SetAltTextAsync(
        [FromForm(Name = StorageKeyField)] string storageKey,
        [FromForm(Name = AltTextField)] string altText,
        UploadedImageRepository uploadedImageRepository
    )
    {
        if (AltText.Read(altText) is not { } read)
        {
            return TypedResults.BadRequest();
        }

        return await uploadedImageRepository.SetAltTextAsync(storageKey, read) ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
