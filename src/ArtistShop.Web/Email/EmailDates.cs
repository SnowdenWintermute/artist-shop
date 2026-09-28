namespace ArtistShop.Web.Email;

using ArtistShop.Web.Utilities;

public static class EmailDates
{
    // as LocalDate first writes it; an email can't learn the reader's time zone
    public static string Text(DateTimeOffset moment) => $"{DateText.Day(moment)} (UTC)";
}
