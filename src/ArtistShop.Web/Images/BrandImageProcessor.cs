using System.Security.Cryptography;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;
using NetVips;

namespace ArtistShop.Web.Images;

public class UnusableImageException(string message) : Exception(message);

// Makes the files a logo or favicon is served as. An SVG is served as uploaded: shown as an image it
// can't run scripts, and opened at its own address the Content-Security-Policy stops them
public class BrandImageProcessor(ImageProcessingLimiter processingLimiter, ImageProcessingSettings processingSettings)
{
    // the nav bar shows a logo this high, in CSS pixels
    public const int LogoHeight = 45;

    // three device pixels per CSS pixel covers phone screens; 1x and 2x screens scale it down
    public const int LogoStoredHeight = LogoHeight * 3;

    public const int FaviconSize = 32;

    // the size iOS asks for on a home screen
    public const int AppleTouchSize = 180;

    // libvips can also read PDFs and other formats whatever the browser called the file, so only the
    // loaders for the types ImageUploadValidation and SVG allow get through
    private static readonly string[] PermittedLoaders = ["jpegload", "pngload", "webpload", "heifload", "tiffload", "svgload"];

    private static readonly double[] Transparent = [0, 0, 0, 0];
    private static readonly double[] White = [255, 255, 255];

    public async Task<List<BrandImageFileContent>> ProcessAsync(
        byte[] original,
        BrandImageType type,
        CancellationToken cancellationToken
    )
    {
        ImageHeader header;
        bool isSvg;

        // reads only the header, as ImageProcessor.ReadHeader does
        using (var image = Image.NewFromBuffer(original))
        {
            var loader = (string)image.Get("vips-loader");

            if (!PermittedLoaders.Any(permitted => loader.StartsWith(permitted, StringComparison.Ordinal)))
            {
                throw new UnsupportedImageFormatException("This file isn't an image format we accept.");
            }

            header = ImageHeader.Of(image);
            isSvg = loader.StartsWith("svgload", StringComparison.Ordinal);
        }

        using var lease = await ImageProcessingAdmission.AcquireAsync(
            header,
            processingLimiter,
            processingSettings,
            cancellationToken
        );

        return type switch
        {
            BrandImageType.Logo or BrandImageType.LogoOnDark => isSvg
                ? [Svg(original, header)]
                : [Logo(original)],
            BrandImageType.Favicon => isSvg
                ? [Svg(original, header), .. FaviconPngs(original, header)]
                : FaviconPngs(original, header),
        };
    }

    private static BrandImageFileContent Svg(byte[] original, ImageHeader header) =>
        Content(BrandImageFileType.Svg, BrandImageFormat.Svg, header.Width, header.Height, original);

    // Transparent margins are trimmed, since logos often arrive with plenty and would look tiny. Never
    // made larger: one shorter than LogoStoredHeight is kept at its own height
    private static BrandImageFileContent Logo(byte[] original)
    {
        using var loaded = Image.NewFromBuffer(original).Autorot();
        using var image = loaded.Colourspace(Enums.Interpretation.Srgb);
        using var trimmed = TrimTransparentMargins(image);

        if (trimmed.Height < LogoHeight)
        {
            throw new UnusableImageException($"Logos must be at least {LogoHeight} pixels high.");
        }

        var height = Math.Min(trimmed.Height, LogoStoredHeight);

        // the width allows a little more than the height needs, so the height is what limits the size
        var width = (int)Math.Ceiling((double)trimmed.Width * height / trimmed.Height) + 1;
        using var resized = trimmed.ThumbnailImage(width, height: height, size: Enums.Size.Down);

        return Content(
            BrandImageFileType.Raster,
            BrandImageFormat.Webp,
            resized.Width,
            resized.Height,
            resized.WriteToBuffer(".webp", new VOption { { "lossless", true }, { "keep", Enums.ForeignKeep.None } })
        );
    }

    private static Image TrimTransparentMargins(Image image)
    {
        if (!image.HasAlpha())
        {
            return image.Copy();
        }

        using var alpha = image[image.Bands - 1];

        // left, top, width and height of what isn't transparent
        var box = alpha.FindTrim(background: [0]);
        var width = Convert.ToInt32(box[2]);
        var height = Convert.ToInt32(box[3]);

        if (width == 0 || height == 0)
        {
            throw new UnusableImageException("This image is completely transparent.");
        }

        return image.ExtractArea(Convert.ToInt32(box[0]), Convert.ToInt32(box[1]), width, height);
    }

    // a non-square image is centred, never cropped. The browser's icon keeps transparency; iOS fills it
    // with black, so the home screen's goes onto white
    private static List<BrandImageFileContent> FaviconPngs(byte[] original, ImageHeader header)
    {
        if (Math.Max(header.Width, header.Height) < FaviconSize)
        {
            throw new UnusableImageException($"Icons must be at least {FaviconSize} pixels wide or high.");
        }

        return
        [
            Content(
                BrandImageFileType.Raster,
                BrandImageFormat.Png,
                FaviconSize,
                FaviconSize,
                Square(original, FaviconSize, flattenOntoWhite: false)
            ),
            Content(
                BrandImageFileType.AppleTouch,
                BrandImageFormat.Png,
                AppleTouchSize,
                AppleTouchSize,
                Square(original, AppleTouchSize, flattenOntoWhite: true)
            ),
        ];
    }

    // ThumbnailBuffer draws an SVG at the size asked for, and shrinks a JPEG as it decodes it
    private static byte[] Square(byte[] original, int size, bool flattenOntoWhite)
    {
        using var thumbnail = Image.ThumbnailBuffer(original, size, height: size, size: Enums.Size.Both);
        using var image = thumbnail.Colourspace(Enums.Interpretation.Srgb);

        using var square = flattenOntoWhite
            ? Flattened(image).Gravity(Enums.CompassDirection.Centre, size, size, Enums.Extend.Background, White)
            : WithAlpha(image).Gravity(Enums.CompassDirection.Centre, size, size, Enums.Extend.Background, Transparent);

        return square.WriteToBuffer(".png", new VOption { { "keep", Enums.ForeignKeep.None } });
    }

    private static Image Flattened(Image image) => image.HasAlpha() ? image.Flatten(background: White) : image;

    private static Image WithAlpha(Image image) => image.HasAlpha() ? image : image.AddAlpha();

    private static BrandImageFileContent Content(
        BrandImageFileType type,
        BrandImageFormat format,
        int width,
        int height,
        byte[] bytes
    ) => new(new BrandImageFile(type, format, width, height, Convert.ToHexStringLower(SHA256.HashData(bytes))), bytes);
}
