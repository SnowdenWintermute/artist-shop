namespace ArtistShop.Web.Utilities;

using System.Globalization;

// dates as they fall in UTC
public static class DateText
{
    // "3 Sep 2026"
    public static string Day(DateTimeOffset moment) =>
        moment.UtcDateTime.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    // "2026-09-03T14:05:09Z", for a <time> element's datetime. Whole seconds: a datetime attribute
    // and JavaScript's Date are only promised up to milliseconds, and "O" writes seven digits of fraction
    public static string DateTimeAttribute(DateTimeOffset moment) =>
        moment.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    // "2026-09-03", for file names, where it sorts by date
    public static string FileNameDay(DateTimeOffset moment) =>
        moment.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
