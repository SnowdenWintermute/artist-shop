using System.Globalization;

namespace ArtistShop.Web.Domain.Catalog;

// h:mm:ss or m:ss, the one spelling of a duration that both the CSV import and the artwork form
// accept. Whole seconds, because that is what the column holds
public static class ArtworkDuration
{
    public const string ExpectedFormat = "h:mm:ss or m:ss";

    public static TimeSpan? TryParse(string text)
    {
        var numbers = text.Split(':')
            .Select(part =>
                int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                    ? number
                    : (int?)null
            )
            .ToList();

        // long, because a large enough hour count overflows an int, and even a TimeSpan
        long? totalSeconds = numbers switch
        {
            [int minutes, int seconds] when seconds < 60 => minutes * 60L + seconds,
            [int hours, int minutes, int seconds] when minutes < 60 && seconds < 60 => hours * 3600L
                + minutes * 60L
                + seconds,
            _ => null,
        };

        // the database stores whole seconds in an int
        if (
            totalSeconds is not long knownSeconds
            || knownSeconds <= 0
            || knownSeconds > int.MaxValue
        )
        {
            return null;
        }

        return TimeSpan.FromSeconds(knownSeconds);
    }

    // under an hour the hours part is left off, so a five minute piece reads 5:00 rather than 0:05:00
    public static string ToText(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes}:{duration.Seconds:00}";
}
