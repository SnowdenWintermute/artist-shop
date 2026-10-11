namespace ArtistShop.Web.Domain.Website;

// the ids are stored in brand_images.type
public enum BrandImageType
{
    Logo = 1,

    // shown instead of Logo on a bar white text reads better on
    LogoOnDark = 2,

    Favicon = 3,
}

// the ids are stored in brand_image_files.type. An SVG upload keeps its SVG as uploaded; a logo has
// a Raster copy only when it wasn't one, and a favicon always has both PNGs, for browsers and iOS
public enum BrandImageFileType
{
    Svg = 1,
    Raster = 2,
    AppleTouch = 3,
}

// the ids are stored in brand_image_files.format
public enum BrandImageFormat
{
    Svg = 1,
    Webp = 2,
    Png = 3,
}

// Sha256 is the file's own, in lower case hex
public record BrandImageFile(BrandImageFileType Type, BrandImageFormat Format, int Width, int Height, string Sha256);

public record BrandImage(BrandImageType Type, string OriginalFileName, IReadOnlyList<BrandImageFile> Files)
{
    public BrandImageFile? File(BrandImageFileType type) => Files.FirstOrDefault(file => file.Type == type);

    // the SVG when there is one, since it stays sharp at any size
    public BrandImageFile Shown =>
        File(BrandImageFileType.Svg)
        ?? File(BrandImageFileType.Raster)
        ?? throw new InvalidOperationException($"The {Type} brand image has neither an SVG nor a raster file.");
}

// the file the artist uploaded, kept on disk under StorageKey. Sha256 is its own, in lower case hex
public record BrandImageOriginal(BrandImageType Type, string StorageKey, string OriginalFileName, string Sha256);
