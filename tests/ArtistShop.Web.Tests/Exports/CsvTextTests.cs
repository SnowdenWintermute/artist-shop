using ArtistShop.Web.Exports;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Exports;

public sealed class CsvTextTests
{
    // CsvTable is what the import reads with, so what it reads back is what the export means
    [Fact]
    public void CsvTableReadsBackEveryCell()
    {
        string[] cells = ["plain", "a, b", "say \"hi\"", "two\nlines", "", "=1+1", "@here", "Oil; Bronze"];
        var csv = new CsvText();
        csv.AddRow(["one", "two", "three", "four", "five", "six", "seven", "eight"]);
        csv.AddRow(cells);

        var table = CsvTable.Parse(csv.ToString());

        Assert.Equal(cells, Assert.Single(table.Rows).Cells);
    }

    [Fact]
    public void OnlyCellsThatNeedThemAreQuoted()
    {
        var csv = new CsvText();
        csv.AddRow(["plain", "a, b", null]);

        Assert.Equal("plain,\"a, b\",\r\n", csv.ToString());
    }

    // a spreadsheet set to split on semicolons or tabs too would otherwise split the list
    [Fact]
    public void ACellWithASemicolonOrTabIsQuoted()
    {
        var csv = new CsvText();
        csv.AddRow(["butts", "Photograph; Screenshot", "a\tb"]);

        Assert.Equal("butts,\"Photograph; Screenshot\",\"a\tb\"\r\n", csv.ToString());
    }

    [Fact]
    public void AFormulaIsWrittenAsText()
    {
        var csv = new CsvText();
        csv.AddRow(["=HYPERLINK(\"http://example.com\")", "-5"]);

        Assert.Equal("\"'=HYPERLINK(\"\"http://example.com\"\")\",\"'-5\"\r\n", csv.ToString());
    }
}
