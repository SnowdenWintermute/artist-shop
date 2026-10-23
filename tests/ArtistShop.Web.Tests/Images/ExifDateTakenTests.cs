using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Images;

namespace ArtistShop.Web.Tests.Images;

public sealed class ExifDateTakenTests
{
    [Fact]
    public void KeepsTheDayAndDropsTheTime()
    {
        Assert.Equal(new PartialDate(new DateOnly(2024, 5, 1), DatePrecision.Day), ExifDateTaken.DayOf("2024:05:01 23:59:59"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0000:00:00 00:00:00")]
    [InlineData("    :  :     :  :  ")]
    [InlineData("2024:02:30 10:00:00")]
    [InlineData("2024-05-01")]
    public void UnsetOrMalformedDatesAreNoDate(string? text)
    {
        Assert.Null(ExifDateTaken.DayOf(text));
    }
}
