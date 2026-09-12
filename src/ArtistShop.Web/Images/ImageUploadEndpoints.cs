using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using NetVips;

namespace ArtistShop.Web.Images;

public record ImageUploadResult(string StorageKey, int Width, int Height, string BlurDataUri);

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
        ImageStoragePaths paths,
        ImageProcessor imageProcessor,
        CancellationToken cancellationToken
    )
    {
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

        // version 7 embeds timestamp when created so they can be sorted by date
        // and avoid database fragmenting if stored there
        // "n" gives 32 hex characters with no dash or braces
        var storageKey = Guid.CreateVersion7().ToString("n");

        // FileStream is IAsyncDisposable
        var originalPath = Path.Combine(paths.Originals, storageKey);

        // putting braces after using makes the using release the resource
        // after the braces, which we need to do before letting Vips
        // process it
        await using (var destination = File.Create(originalPath))
        {
            await file.CopyToAsync(destination, cancellationToken);
        }

        try
        {
            var processed = imageProcessor.Process(storageKey);
            return TypedResults.Ok(
                new ImageUploadResult(
                    storageKey,
                    processed.Width,
                    processed.Height,
                    processed.BlurDataUri
                )
            );
        }
        catch (Exception exception) when (exception is VipsException or ImageTooSmallException)
        {
            File.Delete(originalPath);

            var variantDirectory = Path.Combine(paths.Variants, storageKey);
            if (Directory.Exists(variantDirectory))
            {
                Directory.Delete(variantDirectory, recursive: true);
            }

            return TypedResults.BadRequest(exception.Message);
        }
    }
}
