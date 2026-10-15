using NetVips;

namespace ArtistShop.Web.Images;

public record ProcessedImage(int Width, int Height, string BlurDataUri);

public class ImageTooSmallException(int minimumWidth)
    : Exception($"Images must be at least {minimumWidth} pixels wide.");

public class ImageTooLargeException(string message) : Exception(message);

public class UnsupportedImageFormatException(string message) : Exception(message);

public class ImageProcessor(ImageStorage imageStorage)
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

    public const string UnacceptedFormatMessage = "This file isn't an image format we accept.";

    // NewFromFile only reads the header until pixels are asked for, so this doesn't decode the image
    public ImageHeader ReadHeader(string storageKey)
    {
        using var original = imageStorage.UseLocalOriginal(storageKey);
        using var image = Image.NewFromFile(original.Path);
        var header = ImageHeader.Of(image);

        // only logos and icons may be SVG
        if (header.IsSvg)
        {
            throw new UnsupportedImageFormatException(UnacceptedFormatMessage);
        }

        return header;
    }

    // the standard sizes the image doesn't have yet; one with only its AVIF or only its WebP counts as missing
    public int[] FindMissingSizes(string storageKey)
    {
        var fileNames = imageStorage.VariantFileNames(storageKey).ToHashSet();

        using var original = imageStorage.UseLocalOriginal(storageKey);
        using var source = Image.NewFromFile(original.Path).Autorot();

        return
        [
            .. ImageVariants
                .WidthsFor(source.Width)
                .Where(width =>
                    !fileNames.Contains(ImageVariants.FileName(width, ImageVariantFormat.Avif))
                    || !fileNames.Contains(ImageVariants.FileName(width, ImageVariantFormat.Webp))
                ),
        ];
    }

    // What an upload waits for: the smallest size, which admin lists show, and the blur pages show
    // until a size is ready, both from one small decode. minimumWidth is what the image is for:
    // MinimumSourceWidth for a work, and MinimumPostImageWidth for an image in a post
    public ProcessedImage MakeFirstSize(string storageKey, int minimumWidth)
    {
        using var original = imageStorage.UseLocalOriginal(storageKey);
        using var source = Image.NewFromFile(original.Path).Autorot();
        if (source.Width < minimumWidth)
        {
            throw new ImageTooSmallException(minimumWidth);
        }

        var width = ImageVariants.FirstWidth(source.Width);

        using var firstSize = Resize(Image.Thumbnail(original.Path, width, height: source.Height));
        Save(storageKey, width, firstSize);

        using var blur = firstSize.ThumbnailImage(BlurWidth, outputProfile: "srgb");
        var blurBytes = blur.WriteToBuffer(".webp", new VOption { { "Q", 40 }, { "keep", Enums.ForeignKeep.None } });

        return new ProcessedImage(
            source.Width,
            source.Height,
            $"data:image/webp;base64,{Convert.ToBase64String(blurBytes)}"
        );
    }

    // Only the largest of these is made from the original, and each smaller one from the size before
    // it, so the original is decoded once however many are asked for
    public void MakeSizes(string storageKey, IEnumerable<int> widths)
    {
        using var original = imageStorage.UseLocalOriginal(storageKey);
        using var source = Image.NewFromFile(original.Path).Autorot();

        // null until the largest is made
        Image? previousSize = null;

        try
        {
            foreach (var width in widths.OrderDescending())
            {
                var size = previousSize is null
                    ? Resize(Image.Thumbnail(original.Path, width, height: source.Height))
                    : Resize(previousSize.ThumbnailImage(width, height: previousSize.Height));
                previousSize?.Dispose();
                previousSize = size;

                Save(storageKey, width, size);
            }
        }
        finally
        {
            previousSize?.Dispose();
        }
    }

    // CopyMemory makes libvips do the decode and resize now rather than during the first encode.
    // resized is disposed straight after, so it stops holding the image it was made from
    private static Image Resize(Image resized)
    {
        using (resized)
        {
            return resized.CopyMemory();
        }
    }

    private void Save(string storageKey, int width, Image size)
    {
        imageStorage.SaveVariant(
            storageKey,
            ImageVariants.FileName(width, ImageVariantFormat.Avif),
            size.WriteToBuffer(".avif", new VOption { { "Q", 50 }, { "effort", AvifEffort }, { "keep", VariantMetadata } })
        );

        imageStorage.SaveVariant(
            storageKey,
            ImageVariants.FileName(width, ImageVariantFormat.Webp),
            size.WriteToBuffer(".webp", new VOption { { "Q", 75 }, { "keep", VariantMetadata } })
        );
    }
}
