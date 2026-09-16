using System.Text;

namespace ArtistShop.Web.Imports;

public static class Utf8Text
{
    // throwOnInvalidBytes: without it, bytes from another encoding quietly become "�".
    // encoderShouldEmitUTF8Identifier gives the encoding a byte order mark, which is what makes
    // StreamReader skip one at the start of the file
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);

    // null when the bytes aren't UTF-8. The byte order mark spreadsheets often write is dropped
    public static async Task<string?> TryReadAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, StrictUtf8, detectEncodingFromByteOrderMarks: false);

        try
        {
            return await reader.ReadToEndAsync();
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }
}
