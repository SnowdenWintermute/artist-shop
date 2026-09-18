using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace ArtistShop.Web.Domain.Catalog;

// ": byte" makes the enum's underlying type a byte, which Dapper sends as tinyint to match
// the column. Starting at 1 means default(DatePrecision), which is 0, is never a valid
// precision, so a value that was never set can't quietly pass for "year only".
public enum DatePrecision : byte
{
    Year = 1,
    Month = 2,
    Day = 3,
}

// which input a problem belongs to, so the form can put the message under it
// and the CSV importer can name the column
public enum DatePart : byte
{
    Year = 1,
    Month = 2,
    Day = 3,
}

public record PartialDateError(DatePart Part, string Message);

// kind of vague name, could be confused with system date utilities
public record PartialDate
{
    public DateOnly Date { get; }
    public DatePrecision Precision { get; }
    private const int MostDaysInAnyMonth = 31;

    // with the year unknown, the date might fall in a leap year, so February is
    // given its leap-year length. 2000 is a leap year.
    private const int AnyLeapYear = 2000;

    // each format paired with the precision it produces. ParseExact fills parts the
    // format lacks with 1 as the db's CHECK expects
    // we only accept unambiguous formats because 1/2/2020 and 2/1/2020 mean same thing
    // in different countries, where yyyy-mm-dd is a standard
    private static readonly (string Format, DatePrecision Precision)[] AcceptedFormats =
    [
        ("yyyy", DatePrecision.Year),
        ("yyyy-MM", DatePrecision.Month),
        ("yyyy-MM-dd", DatePrecision.Day),
    ];

    public PartialDate(DateOnly date, DatePrecision precision)
    {
        // don't allow setting the day/month in a date that is not of a precision
        // to know the day/month. Database also checks this.
        var unknownPartsAreSet = precision switch
        {
            DatePrecision.Year => date.Month != 1 || date.Day != 1,
            DatePrecision.Month => date.Day != 1,
            DatePrecision.Day => false,
        };

        if (unknownPartsAreSet)
        {
            throw new ArgumentException(
                $"A {precision} precision date must fall on the first of its period, not {date}."
            );
        }

        Date = date;
        Precision = precision;
    }

    public static PartialDate Parse(string text) =>
        TryParse(text, out var partialDate)
            ? partialDate
            : throw new FormatException($"\"{text}\" is not yyyy, yyyy-MM or yyyy-MM-dd.");

    // Succeeds with a null partialDate when all three parts are blank: "no date" is valid.
    // [NotNullWhen(false)] tells the compiler error is set whenever this returns false.
    public static bool TryFromParts(
        int? year,
        int? month,
        int? day,
        out PartialDate? partialDate,
        [NotNullWhen(false)] out PartialDateError? error
    )
    {
        partialDate = null;
        error = FindErrorInParts(year, month, day);

        if (error is not null)
        {
            return false;
        }
        if (year is int knownYear)
        {
            // the finest part filled in decides the precision
            var precision = (month, day) switch
            {
                (not null, not null) => DatePrecision.Day,
                (not null, null) => DatePrecision.Month,
                _ => DatePrecision.Year,
            };

            partialDate = new PartialDate(new DateOnly(knownYear, month ?? 1, day ?? 1), precision);
        }

        return true;
    }

    // for callers that already validated, like a form after OnValidSubmit
    public static PartialDate? FromParts(int? year, int? month, int? day) =>
        TryFromParts(year, month, day, out var partialDate, out var error)
            ? partialDate
            : throw new ArgumentException(error.Message);

    public static int MaximumDayIn(int? year, int? month)
    {
        // "is < 1 or > 12" is a relational pattern: true when the value is outside 1 to 12
        if (month is not int knownMonth || knownMonth is < 1 or > 12)
        {
            return MostDaysInAnyMonth;
        }

        var yearForLength =
            year is int knownYear && IsSupportedYear(knownYear) ? knownYear : AnyLeapYear;

        return DateTime.DaysInMonth(yearForLength, knownMonth);
    }

    public static string MonthName(int month) =>
        CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);

    public static bool TryParse(string text, [NotNullWhen(true)] out PartialDate? partialDate)
    {
        foreach (var (format, precision) in AcceptedFormats)
        {
            var parsed = DateOnly.TryParseExact(
                text.Trim(),
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date
            );

            if (parsed)
            {
                partialDate = new PartialDate(date, precision);
                return true;
            }
        }
        partialDate = null;
        return false;
    }

    private static bool IsSupportedYear(int year) =>
        year >= DateOnly.MinValue.Year && year <= DateOnly.MaxValue.Year;

    // Checks run from the largest part down, so each check can rely on the ones above it:
    // by the time the day is checked, the year and month are known to be valid.
    private static PartialDateError? FindErrorInParts(int? year, int? month, int? day)
    {
        if (year is not int knownYear)
        {
            return month is null && day is null
                ? null
                : new PartialDateError(DatePart.Year, "A month or day needs a year.");
        }

        if (!IsSupportedYear(knownYear))
        {
            return new PartialDateError(
                DatePart.Year,
                $"The year must be between {DateOnly.MinValue.Year} and {DateOnly.MaxValue.Year}."
            );
        }

        if (month is not int knownMonth)
        {
            return day is null ? null : new PartialDateError(DatePart.Day, "A day needs a month.");
        }

        if (knownMonth is < 1 or > 12)
        {
            return new PartialDateError(DatePart.Month, "The month must be between 1 and 12.");
        }

        if (day is not int knownDay)
        {
            return null;
        }
        var maximumDay = MaximumDayIn(knownYear, knownMonth);

        if (knownDay < 1 || knownDay > maximumDay)
        {
            return new PartialDateError(
                DatePart.Day,
                $"{MonthName(knownMonth)} {knownYear} has {maximumDay} days."
            );
        }

        return null;
    }
}
