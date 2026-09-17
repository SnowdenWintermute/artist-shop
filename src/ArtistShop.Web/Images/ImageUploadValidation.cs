using ArtistShop.Web.Domain;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Images;

public static class ImageUploadValidation
{
    private const long MaximumFileSizeBytes = 25L * Units.BytesPerMebibyte;

    // the whole request: the file plus the multipart form around it. Anything bigger is refused
    // while it is still arriving, before it fills the temp folder
    public const long MaximumRequestBytes = MaximumFileSizeBytes + Units.BytesPerMebibyte;

    private static readonly HashSet<string> PermittedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/avif",
        "image/tiff",
    ];

    // for a file input's accept attribute. Listing the types rather than "image/*" makes the
    // iPhone photo picker convert HEIC photos to JPEG before handing them over
    public static readonly string FileInputAccept = string.Join(",", PermittedContentTypes);

    // client input must be run through GetFileName to sanitize
    // potentially malicious input
    public static string OriginalFileName(IFormFile file) => Path.GetFileName(file.FileName);

    // null when the file may go on to processing
    public static string? FindProblem(IFormFile file)
    {
        if (OriginalFileName(file).Length > CatalogLimits.ArtworkImageFileNameMaximumLength)
        {
            return $"File names must be {CatalogLimits.ArtworkImageFileNameMaximumLength} characters or fewer.";
        }

        if (file.Length is 0)
        {
            return "This file is empty.";
        }

        if (file.Length > MaximumFileSizeBytes)
        {
            return $"Images must be {MaximumFileSizeBytes / Units.BytesPerMebibyte}MB or smaller.";
        }

        if (file.ContentType is "image/heic" or "image/heif")
        {
            return ImageProcessor.UnsupportedHeicMessage;
        }

        // the browser sets this, so it only turns away honest mistakes; libvips reads the
        // actual format from the file's bytes
        if (!PermittedContentTypes.Contains(file.ContentType))
        {
            return $"{file.ContentType} is not an image format we accept.";
        }

        return null;
    }
}
