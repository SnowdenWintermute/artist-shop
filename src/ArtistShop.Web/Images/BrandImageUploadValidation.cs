using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Images;

// a logo or favicon: what a work image may be, or an SVG, and much smaller
public static class BrandImageUploadValidation
{
    private const long MaximumFileSizeBytes = 5L * Units.BytesPerMebibyte;

    private static readonly IReadOnlySet<string> PermittedContentTypes = new HashSet<string>(
        ImageUploadValidation.PermittedContentTypes
    )
    {
        "image/svg+xml",
    };

    public static readonly string FileInputAccept = string.Join(",", PermittedContentTypes);

    public static string? FindProblem(IFormFile file) =>
        ImageUploadValidation.FindProblem(file, MaximumFileSizeBytes, PermittedContentTypes);
}
