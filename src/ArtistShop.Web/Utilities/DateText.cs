namespace ArtistShop.Web.Utilities;

using System.Globalization;

// dates as they fall in UTC
public static class DateText
{
    // "3 Sep 2026"
    public static string Day(DateTimeOffset moment) =>
        moment.UtcDateTime.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    // "2026-09-03", for file names, where it sorts by date
    public static string FileNameDay(DateTimeOffset moment) =>
        moment.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
