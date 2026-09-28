using System.Text;

namespace ArtistShop.Web.Exports;

// Whether a name can be a file's name on Windows, macOS and Linux alike. An export uses the name as
// it is or not at all: a title with a character replaced would no longer match its artwork when the
// images are uploaded again
public static class ExportFileNames
{
    // most file systems allow 255 bytes for one name
    private const int MaximumBytes = 255;

    private static readonly char[] ForbiddenCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    // Windows keeps these for devices, with or without an extension after them
    private static readonly HashSet<string> ReservedNames = new(
        [
            "CON", "PRN", "AUX", "NUL",
            .. Enumerable.Range(0, 10).Select(number => $"COM{number}"),
            .. Enumerable.Range(0, 10).Select(number => $"LPT{number}"),
        ],
        StringComparer.OrdinalIgnoreCase
    );

    public static bool IsPortable(string fileName) =>
        fileName.Trim().Length > 0
        && fileName is not ("." or "..")
        && fileName.IndexOfAny(ForbiddenCharacters) < 0
        && !fileName.Any(char.IsControl)
        // Windows drops a trailing dot or space, so the name would change on the way in
        && !fileName.EndsWith('.')
        && !fileName.EndsWith(' ')
        && !ReservedNames.Contains(fileName.Split('.')[0])
        && Encoding.UTF8.GetByteCount(fileName) <= MaximumBytes;
}
