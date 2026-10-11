using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Images;

public static class BrandImageFormats
{
    public static string ContentType(BrandImageFormat format) =>
        format switch
        {
            BrandImageFormat.Svg => "image/svg+xml",
            BrandImageFormat.Webp => "image/webp",
            BrandImageFormat.Png => "image/png",
        };

    public static string Extension(BrandImageFormat format) =>
        format switch
        {
            BrandImageFormat.Svg => "svg",
            BrandImageFormat.Webp => "webp",
            BrandImageFormat.Png => "png",
        };
}
