using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Sites;
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
// pre-check uses; OneImagelessWork means the image was attached
public record WorkImageMatchResult(
    string WorkName,
    WorkNameMatchType Outcome,
    IReadOnlyList<int> WorkIds
);

public static class ImageUploadEndpoints
{
    // libvips says which decoder failed and names the file on disk; that belongs in the server log,
    // not in front of the artist
    private const string UnreadableImageMessage =
        "We couldn't read this file as an image. It may be damaged, or in a format we don't support.";

    public const string WorkImageUploadPath = "/admin/uploads/work-image";

    // the post editor's script sends its images here
    public const string PostImageUploadPath = "/admin/uploads/post-image";

    // the whole-website import's script sends each image here with the work it's for
    public const string AppendWorkImagePath = "/admin/uploads/work-image-appended";

    public static void MapImageUploadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost(
                WorkImageUploadPath,
                (
                    IFormFile file, // binds the form field named "file", the names must match
                    ImageUploadStore imageUploadStore,
                    HttpResponse response,
                    ILoggerFactory loggerFactory,
                    CancellationToken cancellationToken
                ) =>
                    UploadAsync(
                        file,
                        ImageVariants.MinimumSourceWidth,
                        imageUploadStore,
                        response,
                        loggerFactory,
                        cancellationToken
                    )
            )
            .AsImageUpload();

        endpoints.MapPost("/admin/uploads/work-image-by-name", UploadAndAttachByNameAsync).AsImageUpload();

        endpoints.MapPost(AppendWorkImagePath, UploadAndAppendAsync).AsImageUpload();

        endpoints
            .MapPost(
                PostImageUploadPath,
                (
                    IFormFile file, // as above
                    ImageUploadStore imageUploadStore,
                    HttpResponse response,
                    ILoggerFactory loggerFactory,
                    CancellationToken cancellationToken
                ) =>
                    UploadAsync(
                        file,
                        ImageVariants.MinimumPostImageWidth,
                        imageUploadStore,
                        response,
                        loggerFactory,
                        cancellationToken
                    )
            )
            .AsImageUpload();
    }

    // what every image upload endpoint needs: admin only, rate limited, with room for one image
    private static RouteHandlerBuilder AsImageUpload(this RouteHandlerBuilder endpoint) =>
        endpoint
            // replaces Kestrel's default 30 MB limit for this endpoint only
            .WithMetadata(new RequestSizeLimitAttribute(ImageUploadValidation.MaximumRequestBytes))
            .RequireAuthorization(SitePolicies.Admin)
            .WithMetadata(new ServedOnAttribute(HostTypes.Site))
            .RequireRateLimiting(ImageUploadRateLimiting.PolicyName);

    private static async Task<
        Results<Ok<ImageUploadResult>, ContentHttpResult, StatusCodeHttpResult>
    > UploadAsync(
        IFormFile file,
        int minimumWidth,
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
            var stored = await imageUploadStore.SaveAsync(content, minimumWidth, cancellationToken);
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
        Results<Ok<WorkImageMatchResult>, ContentHttpResult, StatusCodeHttpResult>
    > UploadAndAttachByNameAsync(
        IFormFile file,
        // a form field rather than a route value, so it travels in the same multipart body as the file
        [FromForm] int workTypeId,
        ImageUploadStore imageUploadStore,
        ImageStorage imageStorage,
        WorkImageRepository workImageRepository,
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
        var workName = WorkName.FromFileName(originalFileName);

        if (!workName.CanMatchAnWork)
        {
            return TypedResults.Ok(NoMatch(workName));
        }

        try
        {
            await using var content = file.OpenReadStream();
            var stored = await imageUploadStore.SaveAsync(content, ImageVariants.MinimumSourceWidth, cancellationToken);

            try
            {
                var attached = await workImageRepository.AttachPrimaryImageToImagelessWorkByNameAsync(
                    new WorkTypeId(workTypeId),
                    workName,
                    stored.ToWorkImage(originalFileName),
                    stored.Sha256
                );

                if (!attached.Attached)
                {
                    // nothing references it, and the artist may re-run the folder straight away,
                    // so it goes now rather than waiting days for the sweep
                    imageStorage.Delete(stored.StorageKey);
                }

                return TypedResults.Ok(
                    new WorkImageMatchResult(
                        workName.Value,
                        attached.MatchType,
                        [.. attached.WorkIds.Select(workId => workId.Value)]
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
        catch (ChangedSincePageLoadException exception)
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

    // After the work's other images, unless it has this image already. The page worked out which
    // images the work is missing, but any work on the site is one its admin could add an
    // image to anyway
    // Answers with the image the work now holds, and a 200 even when it already had it, so a
    // retry gets the same answer as the upload whose answer was lost
    private static async Task<
        Results<Ok<WorkImage>, ContentHttpResult, StatusCodeHttpResult>
    > UploadAndAppendAsync(
        IFormFile file,
        // a form field, as above
        [FromForm] int workId,
        ImageUploadStore imageUploadStore,
        ImageStorage imageStorage,
        WorkImageRepository workImageRepository,
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
            await using var content = file.OpenReadStream();
            var stored = await imageUploadStore.SaveAsync(content, ImageVariants.MinimumSourceWidth, cancellationToken);

            try
            {
                var result = await workImageRepository.AppendImageAsync(
                    new WorkId(workId),
                    stored.ToWorkImage(ImageUploadValidation.OriginalFileName(file)),
                    stored.Sha256
                );

                // the work has this image already, as when a retry follows an upload that got
                // in but whose answer was lost, so this copy isn't needed
                if (!result.Appended)
                {
                    imageStorage.Delete(stored.StorageKey);
                }

                return TypedResults.Ok(result.Image);
            }
            catch
            {
                // nothing references it, so it goes now rather than waiting for the sweep
                imageStorage.Delete(stored.StorageKey);
                throw;
            }
        }
        // the artist deleted the work while the import was running
        catch (ChangedSincePageLoadException exception)
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

    private static WorkImageMatchResult NoMatch(WorkName workName) =>
        new(workName.Value, WorkNameMatchType.NoWork, []);

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
