namespace ArtistShop.Web.Utilities;

public static class Units
{
    // file sizes count in powers of two, so "25 MB" is 25 mebibytes
    public const int BytesPerMebibyte = 1024 * 1024;

    // megapixels count in powers of ten
    public const long PixelsPerMegapixel = 1_000_000;
}
