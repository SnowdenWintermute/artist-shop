using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ArtistShop.Web.Images;

public record ImageUploadResult(string StorageKey);

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
        await using var destination = File.Create(Path.Combine(paths.Originals, storageKey));
        await file.CopyToAsync(destination, cancellationToken);

        return TypedResults.Ok(new ImageUploadResult(storageKey));
    }
}
