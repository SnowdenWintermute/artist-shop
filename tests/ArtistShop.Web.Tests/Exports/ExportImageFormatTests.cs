using System.Text;
using ArtistShop.Web.Exports;

namespace ArtistShop.Web.Tests.Exports;

public sealed class ExportImageFormatTests
{
    // an ftyp box: its length, "ftyp", the main brand, a version, then compatible brands
    private static byte[] Ftyp(string mainBrand, params string[] compatibleBrands) =>
        [0, 0, 0, (byte)(16 + (4 * compatibleBrands.Length)), .. Encoding.ASCII.GetBytes("ftyp" + mainBrand), 0, 0, 0, 0, .. Encoding.ASCII.GetBytes(string.Concat(compatibleBrands))];

    public static TheoryData<byte[], string> Headers =>
        new()
        {
            { [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10], ".jpg" },
            { [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0], ".png" },
            { [.. Encoding.ASCII.GetBytes("RIFF"), 1, 2, 3, 4, .. Encoding.ASCII.GetBytes("WEBPVP8 ")], ".webp" },
            { [(byte)'I', (byte)'I', 0x2A, 0, 8, 0, 0, 0], ".tiff" },
            { [(byte)'M', (byte)'M', 0, 0x2A, 0, 0, 0, 8], ".tiff" },
            { Ftyp("avif", "mif1", "miaf"), ".avif" },
            { Ftyp("mif1", "avif"), ".avif" },
        };

    [Theory]
    [MemberData(nameof(Headers))]
    public void ReadsTheFormatFromTheFirstBytes(byte[] header, string extension) =>
        Assert.Equal(extension, ExportImageFormat.Detect(header)?.Extension);

    [Fact]
    public void HeicAndUnknownBytesAreNoFormat()
    {
        Assert.Null(ExportImageFormat.Detect(Ftyp("heic", "mif1", "heic")));
        Assert.Null(ExportImageFormat.Detect(Encoding.ASCII.GetBytes("GIF89a")));
        Assert.Null(ExportImageFormat.Detect([]));
    }
}
