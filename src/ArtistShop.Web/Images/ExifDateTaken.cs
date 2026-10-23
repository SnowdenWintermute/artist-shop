using System.Globalization;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Images;

// one dropped file's date taken, as the drop script read it from the photo
public record ImageDateTaken(string Id, string? DateTaken);

public static class ExifDateTaken
{
    // EXIF writes "2024:05:01 10:00:00" with no time zone, and only the day is kept. A camera whose
    // clock was never set writes "0000:00:00 00:00:00" or blanks, which don't parse, so count as no date
    public static PartialDate? DayOf(string? text) =>
        text is { Length: >= 10 }
        && DateOnly.TryParseExact(text[..10], "yyyy:MM:dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? new PartialDate(day, DatePrecision.Day)
            : null;
}
