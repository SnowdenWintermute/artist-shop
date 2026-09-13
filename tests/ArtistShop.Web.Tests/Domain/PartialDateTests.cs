using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Domain;

public class PartialDateTests
{
    [Theory]
    [InlineData("2019", 2019, 1, 1, DatePrecision.Year)]
    [InlineData("2019-03", 2019, 3, 1, DatePrecision.Month)]
    [InlineData("2019-03-04", 2019, 3, 4, DatePrecision.Day)]
    [InlineData(" 2019 ", 2019, 1, 1, DatePrecision.Year)]
    public void ParsesAcceptedForms(
        string text,
        int year,
        int month,
        int day,
        DatePrecision precision
    )
    {
        Assert.True(PartialDate.TryParse(text, out var partialDate));

        Assert.Equal(new PartialDate(new DateOnly(year, month, day), precision), partialDate);
    }

    [Theory]
    [InlineData("3/4/2019")]
    [InlineData("0")]
    [InlineData("19")]
    [InlineData("2019-3")]
    [InlineData("2019-02-30")]
    [InlineData("")]
    public void RejectsEverythingElse(string text)
    {
        Assert.False(PartialDate.TryParse(text, out _));
    }

    // InlineData accepts null for an int? parameter
    [Theory]
    [InlineData(2019, 2, 28)]
    [InlineData(2020, 2, 29)]
    [InlineData(null, 2, 29)]
    [InlineData(2019, 4, 30)]
    [InlineData(2019, null, 31)]
    public void MaximumDayInKnowsMonthLengths(int? year, int? month, int expected)
    {
        Assert.Equal(expected, PartialDate.MaximumDayIn(year, month));
    }

    [Theory]
    [InlineData(2019, null, null, DatePrecision.Year)]
    [InlineData(2019, 3, null, DatePrecision.Month)]
    [InlineData(2019, 3, 4, DatePrecision.Day)]
    public void FromPartsTakesPrecisionFromFilledParts(
        int year,
        int? month,
        int? day,
        DatePrecision expected
    )
    {
        Assert.True(PartialDate.TryFromParts(year, month, day, out var partialDate, out _));
        Assert.Equal(expected, partialDate?.Precision);
    }

    [Fact]
    public void FromPartsTreatsAllBlankAsNoDate()
    {
        Assert.True(PartialDate.TryFromParts(null, null, null, out var partialDate, out _));
        Assert.Null(partialDate);
    }

    [Theory]
    [InlineData(null, 3, null, DatePart.Year)]
    [InlineData(0, null, null, DatePart.Year)]
    [InlineData(2019, 13, null, DatePart.Month)]
    [InlineData(2019, null, 4, DatePart.Day)]
    [InlineData(2019, 2, 29, DatePart.Day)]
    public void FromPartsBlamesTheRightPart(int? year, int? month, int? day, DatePart expected)
    {
        Assert.False(PartialDate.TryFromParts(year, month, day, out _, out var error));
        Assert.Equal(expected, error.Part);
    }
}
