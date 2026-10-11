using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Images;

// the one place that knows where brand images are served from, like ImageUrls for work images
public static class BrandImageUrls
{
    // BrandImageEndpoints serves the current site's files at this path
    public const string RequestPath = "/brand";

    public static string File(BrandImageFile file) =>
        $"{RequestPath}/{file.Sha256}.{BrandImageFormats.Extension(file.Format)}";
}
