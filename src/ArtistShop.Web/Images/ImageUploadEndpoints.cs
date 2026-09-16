using ArtistShop.Web.Domain;
using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using NetVips;

namespace ArtistShop.Web.Images;

public record ImageUploadResult(
    string StorageKey,
    string? OriginalFileName,
    int Width,
    int Height,
    string BlurDataUri
);

public static class ImageUploadEndpoints
{
    private const long MaximumFileSizeBytes = 25 * 1024 * 1024;

    private static readonly HashSet<string> PermittedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/avif",
        "image/tiff",
    ];

    public static void MapImageUploadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/admin/uploads", UploadAsync)
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
    }

    private static async Task<Results<Ok<ImageUploadResult>, BadRequest<string>>> UploadAsync(
        IFormFile file, // binds the form field named "file", the names must match
        ImageUploadStore imageUploadStore,
        CancellationToken cancellationToken
    )
    {
        // client input must be run through GetFileName to sanitize
        // potentially malicious input
        var originalFileName = Path.GetFileName(file.FileName);
        if (originalFileName.Length > CatalogLimits.ArtworkImageFileNameMaximumLength)
        {
            return TypedResults.BadRequest(
                $"File names must be {CatalogLimits.ArtworkImageFileNameMaximumLength} characters or fewer."
            );
        }

        if (file.Length is 0)
        {
            return TypedResults.BadRequest("Attempted to upload an empty file");
        }

        if (file.Length > MaximumFileSizeBytes)
        {
            return TypedResults.BadRequest(
                $"Images must be {MaximumFileSizeBytes / 1024 / 1024}MB or smaller."
            );
        }

        if (!PermittedContentTypes.Contains(file.ContentType))
        {
            return TypedResults.BadRequest($"{file.ContentType} is not an image format we accept.");
        }

        try
        {
            // OpenReadStream reads the uploaded bytes as a stream rather than all at once
            await using var content = file.OpenReadStream();
            var stored = await imageUploadStore.SaveAsync(content, cancellationToken);
            return TypedResults.Ok(
                new ImageUploadResult(
                    stored.StorageKey,
                    originalFileName,
                    stored.Processed.Width,
                    stored.Processed.Height,
                    stored.Processed.BlurDataUri
                )
            );
        }
        catch (Exception exception) when (exception is VipsException or ImageTooSmallException)
        {
            return TypedResults.BadRequest(exception.Message);
        }
    }
}
