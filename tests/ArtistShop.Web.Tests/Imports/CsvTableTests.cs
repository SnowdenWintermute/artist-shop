using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Imports;

public sealed class CsvTableTests
{
    [Fact]
    public void ReadsTrimmedHeadersAndCells()
    {
        var table = CsvTable.Parse("title , price\n Sunset ,400\n");

        Assert.Equal(["title", "price"], table.Headers);
        Assert.Equal(["Sunset", "400"], Assert.Single(table.Rows).Cells);
    }

    [Fact]
    public void ReadsQuotedCells()
    {
        var table = CsvTable.Parse(
            "title,series,description\n"
                + "Dawn,\"Sunrise, Sunset\",\"She said \"\"hello\"\"\nand left\"\n"
        );

        Assert.Equal(
            ["Dawn", "Sunrise, Sunset", "She said \"hello\"\nand left"],
            Assert.Single(table.Rows).Cells
        );
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void ReadsEveryLineBreakAsNewline(string lineBreak)
    {
        var table = CsvTable.Parse($"title,description{lineBreak}Dawn,\"two{lineBreak}lines\"{lineBreak}");

        Assert.Equal(["Dawn", "two\nlines"], Assert.Single(table.Rows).Cells);
    }

    // a spreadsheet shows a cell with a line break as one row, and a blank line as a blank row
    [Fact]
    public void NumbersRowsAsASpreadsheetDoes()
    {
        var table = CsvTable.Parse("title,description\nA,\"two\nlines\"\nB,x\n\nC,y\n");

        Assert.Equal([2, 3, 5], table.Rows.Select(row => row.RowNumber));
    }

    [Fact]
    public void SkipsBlankRows()
    {
        var table = CsvTable.Parse("title,price\n\n , \nSunset,400\n,,\n");

        Assert.Equal(["Sunset"], table.Rows.Select(row => row.Cells[0]));
    }

    [Fact]
    public void FillsAShortRowWithBlanks()
    {
        var table = CsvTable.Parse("title,price,sold\nSunset\n");

        Assert.Equal(["Sunset", "", ""], Assert.Single(table.Rows).Cells);
    }

    [Fact]
    public void IgnoresBlankCellsPastTheHeaders()
    {
        var table = CsvTable.Parse("title,price\nSunset,400,,\n");

        Assert.Equal(["Sunset", "400"], Assert.Single(table.Rows).Cells);
    }

    [Fact]
    public void RejectsACellPastTheHeaders()
    {
        var exception = Assert.Throws<MalformedCsvException>(() =>
            CsvTable.Parse("title,price\nSunset,400\nDawn,300,extra\n")
        );

        Assert.Equal(3, exception.RowNumber);
    }

    [Fact]
    public void RejectsAQuoteThatIsNeverClosed()
    {
        var exception = Assert.Throws<MalformedCsvException>(() =>
            CsvTable.Parse("title,price\nSunset,400\n\"Dawn,300\n")
        );

        Assert.Equal(3, exception.RowNumber);
    }

    [Fact]
    public void KeepsDuplicateHeaders()
    {
        var table = CsvTable.Parse("title,Title\nSunset,Dawn\n");

        Assert.Equal(["title", "Title"], table.Headers);
    }

    [Fact]
    public void ReadsEmptyTextAsNothing()
    {
        var table = CsvTable.Parse("");

        Assert.Empty(table.Headers);
        Assert.Empty(table.Rows);
    }

    // our series separator must not be mistaken for the column separator
    [Fact]
    public void OnlySplitsOnCommas()
    {
        var table = CsvTable.Parse("title;series\nSunset;Gardens\n");

        Assert.Equal(["title;series"], table.Headers);
    }
}
