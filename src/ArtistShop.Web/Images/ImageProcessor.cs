using NetVips;

namespace ArtistShop.Web.Images;

public record ProcessedImage(int Width, int Height, string BlurDataUri);

// DecodedBytes is the size of the pixels once decoded, before any working copies
public record ImageHeader(int Width, int Height, long DecodedBytes);

public class ImageTooSmallException(int minimumWidth)
    : Exception($"Images must be at least {minimumWidth} pixels wide.");

public class ImageTooLargeException(string message) : Exception(message);

public class UnsupportedImageFormatException(string message) : Exception(message);

public class ImageProcessor(ImageStorage imageStorage)
{
    private const int BlurWidth = 20;

    // ForeignKeep is a set of flags (a "bitfield"), so Icc on its own means "keep only the colour
    // profile": no Exif, no Xmp, no Iptc, so no camera gps coordinates on a publicly served variant
    private const Enums.ForeignKeep VariantMetadata = Enums.ForeignKeep.Icc;

    public const string UnsupportedHeicMessage =
        "HEIC photos (the iPhone default) aren't supported. Export them as JPEG or another compatible format first.";

    // NewFromFile only reads the header until pixels are asked for, so this doesn't decode the image.
    // The decoder allocates what the header states, so a lying header can't make the decode bigger
    public ImageHeader ReadHeader(string storageKey)
    {
        using var image = Image.NewFromFile(imageStorage.OriginalPath(storageKey));

        // HEIC and AVIF share a container, and the header names the codec inside it. Our libvips can't
        // decode HEVC (it is patent-encumbered), so HEIC is turned away here instead of failing mid-decode
        if (image.Contains("heif-compression") && (string)image.Get("heif-compression") == "hevc")
        {
            throw new UnsupportedImageFormatException(UnsupportedHeicMessage);
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

        return new ImageHeader(image.Width, image.Height, decodedBytes);
    }

    public ProcessedImage Process(string storageKey)
    {
        var originalPath = imageStorage.OriginalPath(storageKey);
        using var source = Image.NewFromFile(originalPath).Autorot();
        if (source.Width < ImageVariants.MinimumSourceWidth)
        {
            throw new ImageTooSmallException(ImageVariants.MinimumSourceWidth);
        }

        var fittingWidths = ImageVariants.WidthsFor(source.Width);

        var variantDirectory = imageStorage.VariantDirectory(storageKey);
        Directory.CreateDirectory(variantDirectory);

        foreach (var width in fittingWidths)
        {
            using var variant = Image
                .Thumbnail(originalPath, width, height: source.Height)
                .CopyMemory();
            variant.WriteToFile(
                Path.Combine(variantDirectory, ImageVariants.FileName(width, ImageVariantFormat.Avif)),
                new VOption { { "Q", 50 }, { "keep", VariantMetadata } }
            );
            variant.WriteToFile(
                Path.Combine(variantDirectory, ImageVariants.FileName(width, ImageVariantFormat.Webp)),
                new VOption { { "Q", 75 }, { "keep", VariantMetadata } }
            );
        }

        using var blur = Image.Thumbnail(originalPath, BlurWidth, outputProfile: "srgb");

        var blurBytes = blur.WriteToBuffer(
            ".webp",
            new VOption { { "Q", 40 }, { "keep", Enums.ForeignKeep.None } }
        );

        return new ProcessedImage(
            source.Width,
            source.Height,
            $"data:image/webp;base64,{Convert.ToBase64String(blurBytes)}"
        );
    }
}
