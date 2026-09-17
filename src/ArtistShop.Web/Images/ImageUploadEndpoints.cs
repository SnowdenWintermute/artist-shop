using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
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
    public static void MapImageUploadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/admin/uploads", UploadAsync)
            // replaces Kestrel's default 30 MB limit for this endpoint only
            .WithMetadata(new RequestSizeLimitAttribute(ImageUploadValidation.MaximumRequestBytes))
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
    }

    private static async Task<
        Results<Ok<ImageUploadResult>, BadRequest<string>, StatusCodeHttpResult>
    > UploadAsync(
        IFormFile file, // binds the form field named "file", the names must match
        ImageUploadStore imageUploadStore,
        HttpResponse response,
        CancellationToken cancellationToken
    )
    {
        if (ImageUploadValidation.FindProblem(file) is string problem)
        {
            return TypedResults.BadRequest(problem);
        }

        try
        {
            // OpenReadStream reads the uploaded bytes as a stream rather than all at once
            await using var content = file.OpenReadStream();
            var stored = await imageUploadStore.SaveAsync(content, cancellationToken);
            return TypedResults.Ok(
                new ImageUploadResult(
                    stored.StorageKey,
                    ImageUploadValidation.OriginalFileName(file),
                    stored.Processed.Width,
                    stored.Processed.Height,
                    stored.Processed.BlurDataUri
                )
            );
        }
        catch (Exception exception) when (IsRejectedImage(exception))
        {
            return TypedResults.BadRequest(exception.Message);
        }
        catch (ImageProcessingBusyException exception)
        {
            return Busy(response, exception);
        }
    }

    private static bool IsRejectedImage(Exception exception) =>
        exception
            is VipsException
                or ImageTooSmallException
                or ImageTooLargeException
                or UnsupportedImageFormatException;

    // 503 tells the client the server is overloaded rather than that the request was wrong, and
    // Retry-After says how many seconds to wait before sending the file again
    private static StatusCodeHttpResult Busy(HttpResponse response, ImageProcessingBusyException exception)
    {
        response.Headers.RetryAfter = ((int)exception.RetryAfter.TotalSeconds).ToString();
        return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}
