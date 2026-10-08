using System.Diagnostics;
using NetVips;

namespace ArtistShop.Web.Images;

public record ProcessedImage(int Width, int Height, string BlurDataUri);

// DecodedBytes is the size of the pixels once decoded, before any working copies
public record ImageHeader(int Width, int Height, long DecodedBytes);

public class ImageTooSmallException(int minimumWidth)
    : Exception($"Images must be at least {minimumWidth} pixels wide.");

public class ImageTooLargeException(string message) : Exception(message);

public class UnsupportedImageFormatException(string message) : Exception(message);

public class ImageProcessor(ImageStorage imageStorage, ILogger<ImageProcessor> logger)
{
    private const int BlurWidth = 20;

    // how hard the AVIF encoder searches, 0 to 9. The default of 4 took 13 to 19 seconds per 1600 pixel
    // image on the one-CPU server; 1 was about 25 times faster for files about 8% bigger
    private const int AvifEffort = 1;

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

    // minimumWidth is what the image is for: MinimumSourceWidth for an artwork, and
    // MinimumPostImageWidth for an image in a post
    public ProcessedImage Process(string storageKey, int minimumWidth)
    {
        var started = Stopwatch.GetTimestamp();
        var originalPath = imageStorage.OriginalPath(storageKey);
        using var source = Image.NewFromFile(originalPath).Autorot();
        if (source.Width < minimumWidth)
        {
            throw new ImageTooSmallException(minimumWidth);
        }

        var fittingWidths = ImageVariants.WidthsFor(source.Width);

        var variantDirectory = imageStorage.VariantDirectory(storageKey);
        Directory.CreateDirectory(variantDirectory);

        // milliseconds per step, logged below to see where processing time goes
        var stepTimings = new List<string>();

        // only the largest copy is made from the original, and each smaller one from the copy before
        // it, so the original is decoded once. Null until the largest copy is made
        Image? previousVariant = null;
        byte[] blurBytes;

        try
        {
            // Enumerable.Reverse, because an array's own Reverse reverses it in place
            foreach (var width in Enumerable.Reverse(fittingWidths))
            {
                var stepStarted = Stopwatch.GetTimestamp();
                // CopyMemory makes libvips do the decode and resize now rather than during the first encode
                var variant = (
                    previousVariant is null
                        ? Image.Thumbnail(originalPath, width, height: source.Height)
                        : previousVariant.ThumbnailImage(width, height: previousVariant.Height)
                ).CopyMemory();
                previousVariant?.Dispose();
                previousVariant = variant;
                var resizeMilliseconds = ElapsedMilliseconds(ref stepStarted);

                variant.WriteToFile(
                    Path.Combine(variantDirectory, ImageVariants.FileName(width, ImageVariantFormat.Avif)),
                    new VOption { { "Q", 50 }, { "effort", AvifEffort }, { "keep", VariantMetadata } }
                );
                var avifMilliseconds = ElapsedMilliseconds(ref stepStarted);

                variant.WriteToFile(
                    Path.Combine(variantDirectory, ImageVariants.FileName(width, ImageVariantFormat.Webp)),
                    new VOption { { "Q", 75 }, { "keep", VariantMetadata } }
                );
                var webpMilliseconds = ElapsedMilliseconds(ref stepStarted);

                stepTimings.Add($"{width}: resize {resizeMilliseconds} avif {avifMilliseconds} webp {webpMilliseconds}");
            }

            if (previousVariant is null)
            {
                throw new InvalidOperationException($"No variant widths fit an image {source.Width} pixels wide.");
            }

            var blurStarted = Stopwatch.GetTimestamp();
            // made from the smallest copy, the last one made
            using var blur = previousVariant.ThumbnailImage(BlurWidth, outputProfile: "srgb");

            blurBytes = blur.WriteToBuffer(
                ".webp",
                new VOption { { "Q", 40 }, { "keep", Enums.ForeignKeep.None } }
            );
            stepTimings.Add($"blur {ElapsedMilliseconds(ref blurStarted)}");
        }
        finally
        {
            previousVariant?.Dispose();
        }

#pragma warning disable CA1873
        logger.LogInformation(
            "Processed image {StorageKey} ({Loader}, {Width}x{Height}) in {TotalMilliseconds} ms; steps in ms: {StepTimings}",
            storageKey,
            source.Get("vips-loader"),
            source.Width,
            source.Height,
            (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            string.Join("; ", stepTimings)
        );
#pragma warning restore CA1873

        return new ProcessedImage(
            source.Width,
            source.Height,
            $"data:image/webp;base64,{Convert.ToBase64String(blurBytes)}"
        );
    }

    // returns the milliseconds since stepStarted, and restarts it for the next step
    private static long ElapsedMilliseconds(ref long stepStarted)
    {
        var now = Stopwatch.GetTimestamp();
        var milliseconds = (long)Stopwatch.GetElapsedTime(stepStarted, now).TotalMilliseconds;
        stepStarted = now;
        return milliseconds;
    }
}
