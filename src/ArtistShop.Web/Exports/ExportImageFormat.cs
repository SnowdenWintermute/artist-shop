using System.Text;

namespace ArtistShop.Web.Exports;

// An original's format, read from its first bytes: originals are stored under a key with no
// extension, and the name it was uploaded with may not match what's inside
public record ExportImageFormat(string Extension, bool IsCompressed)
{
    // enough for AVIF's first box and the brands listed in it
    public const int HeaderLength = 32;

    // already compressed, so deflating them again costs time and saves almost nothing
    public static readonly ExportImageFormat Jpeg = new(".jpg", IsCompressed: true);
    public static readonly ExportImageFormat Png = new(".png", IsCompressed: true);
    public static readonly ExportImageFormat Webp = new(".webp", IsCompressed: true);
    public static readonly ExportImageFormat Avif = new(".avif", IsCompressed: true);
    public static readonly ExportImageFormat Tiff = new(".tiff", IsCompressed: false);

    // the longest extension any format gets, so a name checked with it fits with every one
    public const string LongestExtension = ".webp";

    // null for bytes that aren't one of the formats uploads accept
    public static ExportImageFormat? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith((byte[])[0xFF, 0xD8, 0xFF]))
        {
            return Jpeg;
        }

        if (header.StartsWith((byte[])[0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return Png;
        }

        if (header.Length >= 12 && Ascii(header[..4]) == "RIFF" && Ascii(header[8..12]) == "WEBP")
        {
            return Webp;
        }

        // little-endian "II*" and big-endian "MM" then 42
        if (header.StartsWith((byte[])[(byte)'I', (byte)'I', 0x2A, 0x00]) || header.StartsWith((byte[])[(byte)'M', (byte)'M', 0x00, 0x2A]))
        {
            return Tiff;
        }

        return IsAvif(header) ? Avif : null;
    }

    // AVIF and HEIC share a container that starts with an "ftyp" box listing brands: the main one,
    // a version, then the compatible ones. Only an AVIF lists "avif" or "avis" (an animated one)
    private static bool IsAvif(ReadOnlySpan<byte> header)
    {
        if (header.Length < 16 || Ascii(header[4..8]) != "ftyp")
        {
            return false;
        }

        var boxLength = Math.Min(header.Length, (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3]);
        var brands = new List<string> { Ascii(header[8..12]) };

        for (var start = 16; start + 4 <= boxLength; start += 4)
        {
            brands.Add(Ascii(header[start..(start + 4)]));
        }

        return brands.Any(brand => brand is "avif" or "avis");
    }

    private static string Ascii(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes);
}
