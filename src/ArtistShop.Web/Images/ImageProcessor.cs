using NetVips;

namespace ArtistShop.Web.Images;

public record ProcessedImage(int Width, int Height, string BlurDataUri);

public class ImageTooSmallException(int minimumWidth)
    : Exception($"Images must be at least {minimumWidth} pixels wide.");

public class ImageProcessor(ImageStoragePaths paths)
{
    // @TODO once frontend gallery grid exists, measure the size of the elements
    // and derive these values from it
    public static readonly int[] VariantWidths = [400, 800, 1600];

    private const int BlurWidth = 20;

    public ProcessedImage Process(string storageKey)
    {
        var originalPath = Path.Combine(paths.Originals, storageKey);
        var source = Image.NewFromFile(originalPath).Autorot();
        var fittingWidths = VariantWidths.Where(width => width <= source.Width).ToArray();

        if (fittingWidths.Length is 0)
        {
            throw new ImageTooSmallException(VariantWidths.Min());
        }

        var variantDirectory = Path.Combine(paths.Variants, storageKey);
        Directory.CreateDirectory(variantDirectory);

        foreach (var width in fittingWidths)
        {
            // keeping International Color Consortium metadata but
            // stripping everything else to protect against people
            // reading gps coords metadata from uploaded images
            // blur uri strips all metadata
            using var variant = Image.Thumbnail(originalPath, width).CopyMemory();
            var exifMetadataDesired = Enums.ForeignKeep.Icc | Enums.ForeignKeep.Xmp;
            variant.WriteToFile(
                Path.Combine(variantDirectory, $"{width}.avif"),
                new VOption { { "Q", 50 }, { "keep", exifMetadataDesired } }
            );
            variant.WriteToFile(
                Path.Combine(variantDirectory, $"{width}.webp"),
                new VOption { { "Q", 75 }, { "keep", exifMetadataDesired } }
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
