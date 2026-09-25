using System.Globalization;
using System.Text.RegularExpressions;
using ArtistShop.Web.Domain.Publishing;

namespace ArtistShop.Web.Images;

public enum ImageVariantFormat : byte
{
    Avif = 1,
    Webp = 2,
}

// the resized copies ImageProcessor writes for every upload, and how they are named
public static partial class ImageVariants
{
    public static readonly int[] Widths = [160, 400, 800, 1600];

    // the narrowest image worth keeping for an artwork, not the narrowest variant: the admin
    // thumbnail is small enough that Widths[0] would let in images too small for the gallery
    public const int MinimumSourceWidth = 400;

    // a post takes an image of any width, and the page stretches it to the size picked
    public const int MinimumPostImageWidth = 1;

    // the small preview the admin lists and the upload rows show, at twice its 80 pixel box
    public const int AdminThumbnailWidth = 160;

    // the cap on what a browse tile asks for; the browser picks from the srcset by how wide
    // the tile actually came out
    public const int CardWidth = 800;

    // the picture in the card a chat app shows for a pasted link, which is never wide
    public const int LinkPreviewWidth = 800;

    // the image an artwork page is built around may ask for anything there is. [^1] is the
    // last item of the array
    public static readonly int MainImageWidth = Widths[^1];

    // an image embedded in a post. Every image has both, since none narrower than
    // MinimumSourceWidth is kept
    public static int EmbedWidth(EmbedImageSize size) =>
        size switch
        {
            EmbedImageSize.Small => Widths[0],
            EmbedImageSize.Medium => Widths[1],
        };

    public static string FileName(int width, ImageVariantFormat format) =>
        FileName(width.ToString(CultureInfo.InvariantCulture), format);

    // takes a placeholder for a script to put a width in, as well as a width
    public static string FileName(string width, ImageVariantFormat format) =>
        $"{width}.{Extension(format)}";

    // the format of one of FileName's names, such as 800.avif; null for any other name
    public static ImageVariantFormat? ReadFileName(string fileName)
    {
        var match = FileNamePattern().Match(fileName);

        if (!match.Success)
        {
            return null;
        }

        foreach (var format in Enum.GetValues<ImageVariantFormat>())
        {
            if (Extension(format) == match.Groups["extension"].Value)
            {
                return format;
            }
        }

        return null;
    }

    public static string ContentType(ImageVariantFormat format) =>
        format switch
        {
            ImageVariantFormat.Avif => "image/avif",
            ImageVariantFormat.Webp => "image/webp",
        };

    private static string Extension(ImageVariantFormat format) =>
        format switch
        {
            ImageVariantFormat.Avif => "avif",
            ImageVariantFormat.Webp => "webp",
        };

    // \z rather than $, which also allows a trailing line break
    [GeneratedRegex(@"\A[0-9]+\.(?<extension>[a-z]+)\z")]
    private static partial Regex FileNamePattern();

    // Variants are never upscaled, so an image only has the widths up to its own. One narrower
    // than the medium embed, which only a post takes, also gets a copy at its own width, so an
    // embed stretches every pixel there is rather than a smaller variant. For a post image, then,
    // the file an embed shows is Math.Min(the embed's width, the image's), which
    // PostImageEmbed.razor.js relies on
    public static int[] WidthsFor(int imageWidth) =>
        imageWidth < EmbedWidth(EmbedImageSize.Medium) && !Widths.Contains(imageWidth)
            ? [.. Widths.Where(width => width < imageWidth), imageWidth]
            : [.. Widths.Where(width => width <= imageWidth)];

    // the widest file there is of the image. A post image's embed only opens the lightbox when this
    // is wider than the size it's shown at, as PostImageEmbed.razor.js works out for its checkbox
    public static int LargestWidthFor(int imageWidth) => WidthsFor(imageWidth)[^1];

    // the largest variant that exists and isn't wider than wanted; the smallest variant when even
    // that is wider than wanted
    public static int LargestWidthUpTo(int imageWidth, int wantedWidth)
    {
        var existingWidths = WidthsFor(imageWidth);

        if (existingWidths.Length is 0)
        {
            // ImageProcessor turns away images narrower than the smallest variant
            throw new InvalidOperationException(
                $"An image {imageWidth} pixels wide has no variants."
            );
        }

        return existingWidths.LastOrDefault(width => width <= wantedWidth, existingWidths[0]);
    }
}
