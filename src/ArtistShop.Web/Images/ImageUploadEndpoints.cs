using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NetVips;

namespace ArtistShop.Web.Images;

public record ImageUploadResult(
    string StorageKey,
    string OriginalFileName,
    int Width,
    int Height,
    string BlurDataUri
);

// what the bulk page's report shows for one file. Outcome is the same classification the
// pre-check uses; OneImagelessArtwork means the image was attached
public record ArtworkImageMatchResult(
    string ArtworkName,
    ArtworkNameMatchType Outcome,
    IReadOnlyList<int> ArtworkIds
);

public static class ImageUploadEndpoints
{
    // libvips says which decoder failed and names the file on disk; that belongs in the server log,
    // not in front of the artist
    private const string UnreadableImageMessage =
        "We couldn't read this file as an image. It may be damaged, or in a format we don't support.";

    public static void MapImageUploadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/admin/uploads", UploadAsync)
            // replaces Kestrel's default 30 MB limit for this endpoint only
            .WithMetadata(new RequestSizeLimitAttribute(ImageUploadValidation.MaximumRequestBytes))
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin))
            .RequireRateLimiting(ImageUploadRateLimiting.PolicyName);

        endpoints
            .MapPost("/admin/uploads/artwork-image-by-name", UploadAndAttachByNameAsync)
            .WithMetadata(new RequestSizeLimitAttribute(ImageUploadValidation.MaximumRequestBytes))
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin))
            .RequireRateLimiting(ImageUploadRateLimiting.PolicyName);
    }

    private static async Task<
        Results<Ok<ImageUploadResult>, ContentHttpResult, StatusCodeHttpResult>
    > UploadAsync(
        IFormFile file, // binds the form field named "file", the names must match
        ImageUploadStore imageUploadStore,
        HttpResponse response,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken
    )
    {
        if (ImageUploadValidation.FindProblem(file) is string problem)
        {
            return Rejected(problem);
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
        catch (VipsException exception)
        {
            loggerFactory
                .CreateLogger(typeof(ImageUploadEndpoints))
                .LogWarning(exception, "libvips could not read an uploaded image.");

            return Rejected(UnreadableImageMessage);
        }
        catch (Exception exception) when (IsRejectedImage(exception))
        {
            return Rejected(exception.Message);
        }
        catch (ImageProcessingBusyException exception)
        {
            return Busy(response, exception);
        }
    }

    private static async Task<
        Results<Ok<ArtworkImageMatchResult>, ContentHttpResult, StatusCodeHttpResult>
    > UploadAndAttachByNameAsync(
        IFormFile file,
        // a form field rather than a route value, so it travels in the same multipart body as the file
        [FromForm] int artworkTypeId,
        ImageUploadStore imageUploadStore,
        ImageStorage imageStorage,
        ArtworkImageRepository artworkImageRepository,
        HttpResponse response,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken
    )
    {
        if (ImageUploadValidation.FindProblem(file) is string problem)
        {
            return Rejected(problem);
        }

        var originalFileName = ImageUploadValidation.OriginalFileName(file);
        var artworkName = ArtworkName.FromFileName(originalFileName);

        if (!artworkName.CanMatchAnArtwork)
        {
            return TypedResults.Ok(NoMatch(artworkName));
        }

        try
        {
            await using var content = file.OpenReadStream();
            var stored = await imageUploadStore.SaveAsync(content, cancellationToken);

            try
            {
                var attached = await artworkImageRepository.AttachPrimaryImageToImagelessArtworkByNameAsync(
                    new ArtworkTypeId(artworkTypeId),
                    artworkName,
                    new ArtworkImage(
                        stored.StorageKey,
                        originalFileName,
                        stored.Processed.Width,
                        stored.Processed.Height,
                        stored.Processed.BlurDataUri
                    )
                );

                if (!attached.Attached)
                {
                    // nothing references it, and the artist may re-run the folder straight away,
                    // so it goes now rather than waiting days for the sweep
                    imageStorage.Delete(stored.StorageKey);
                }

                return TypedResults.Ok(
                    new ArtworkImageMatchResult(
                        artworkName.Value,
                        attached.MatchType,
                        [.. attached.ArtworkIds.Select(artworkId => artworkId.Value)]
                    )
                );
            }
            catch
            {
                imageStorage.Delete(stored.StorageKey);
                throw;
            }
        }
        // the artist deleted the work type while the run was going: every remaining file is doomed
        catch (CatalogChangedException exception)
        {
            return Rejected(exception.Message);
        }
        catch (VipsException exception)
        {
            loggerFactory
                .CreateLogger(typeof(ImageUploadEndpoints))
                .LogWarning(exception, "libvips could not read an uploaded image.");

            return Rejected(UnreadableImageMessage);
        }
        catch (Exception exception) when (IsRejectedImage(exception))
        {
            return Rejected(exception.Message);
        }
        catch (ImageProcessingBusyException exception)
        {
            return Busy(response, exception);
        }
    }

    private static ArtworkImageMatchResult NoMatch(ArtworkName artworkName) =>
        new(artworkName.Value, ArtworkNameMatchType.NoArtwork, []);

    // these carry a message written for the artist; libvips's own messages are caught above
    private static bool IsRejectedImage(Exception exception) =>
        exception is ImageTooSmallException or ImageTooLargeException or UnsupportedImageFormatException;

    // plain text (Text's default, in UTF-8), because that's what the upload pages show the artist.
    // TypedResults.BadRequest would send it as JSON, which they treat as an unexplained failure
    private static ContentHttpResult Rejected(string message) =>
        TypedResults.Text(message, statusCode: StatusCodes.Status400BadRequest);

    // 503 tells the client the server is overloaded rather than that the request was wrong, and
    // Retry-After says how many seconds to wait before sending the file again
    private static StatusCodeHttpResult Busy(HttpResponse response, ImageProcessingBusyException exception)
    {
        response.Headers.RetryAfter = ((int)exception.RetryAfter.TotalSeconds).ToString();
        return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}
