using NetVips;

namespace ArtistShop.Web.Images;

// DecodedBytes is the size of the pixels once decoded, before any working copies. Only brand images
// accept an SVG, so the rest refuse one with IsSvg
public record ImageHeader(int Width, int Height, long DecodedBytes, bool IsSvg)
{
    // libvips can also read PDFs and other formats whatever the browser called the file, so only the
    // loaders for the types ImageUploadValidation and BrandImageUploadValidation allow get through
    private static readonly string[] PermittedLoaders = ["jpegload", "uhdrload", "pngload", "webpload", "heifload", "tiffload", "svgload"];

    // image must be opened but not yet decoded, as NewFromFile and NewFromBuffer leave it.
    // The decoder allocates what the header states, so a lying header can't make the decode bigger
    public static ImageHeader Of(Image image)
    {
        var loader = (string)image.Get("vips-loader");

        if (!PermittedLoaders.Any(permitted => loader.StartsWith(permitted, StringComparison.Ordinal)))
        {
            throw new UnsupportedImageFormatException(ImageProcessor.UnacceptedFormatMessage);
        }

        // HEIC and AVIF share a container, and the header names the codec inside it. Our libvips can't
        // decode HEVC (it is patent-encumbered), so HEIC is turned away here instead of failing mid-decode
        if (image.Contains("heif-compression") && (string)image.Get("heif-compression") == "hevc")
        {
            throw new UnsupportedImageFormatException(ImageProcessor.UnsupportedHeicMessage);
        }

        var bytesPerSample = image.Format switch
        {
            Enums.BandFormat.Uchar or Enums.BandFormat.Char => 1,
            Enums.BandFormat.Ushort or Enums.BandFormat.Short => 2,
            Enums.BandFormat.Uint or Enums.BandFormat.Int or Enums.BandFormat.Float => 4,
            Enums.BandFormat.Double or Enums.BandFormat.Complex => 8,
            Enums.BandFormat.Dpcomplex => 16,
            _ => throw new InvalidOperationException($"Unexpected band format {image.Format}."),
        };

        // long, because a large image's byte count overflows int
        var decodedBytes = (long)image.Width * image.Height * image.Bands * bytesPerSample;

        return new ImageHeader(
            image.Width,
            image.Height,
            decodedBytes,
            loader.StartsWith("svgload", StringComparison.Ordinal)
        );
    }
}
