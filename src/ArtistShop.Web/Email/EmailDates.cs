namespace ArtistShop.Web.Email;

using System.Globalization;

public static class EmailDates
{
    // as LocalDate first writes it; an email can't learn the reader's time zone
    public static string Text(DateTimeOffset moment) =>
        $"{moment.UtcDateTime.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} (UTC)";
}
