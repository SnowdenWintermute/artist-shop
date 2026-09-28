using System.Globalization;
using CsvHelper.Configuration;

namespace ArtistShop.Web.Exports;

// CSV the way spreadsheets and our own CsvTable read it. A cell starting with =, @, +, - or a tab
// gets a ' in front, so a spreadsheet shows it as text instead of running it as a formula someone
// typed into a title; CsvTable takes the ' off again
public class CsvText
{
    // what spreadsheets also split on besides commas: LibreOffice can be set to split on all three, and
    // Excel uses ; in much of Europe. Quoted, a list like "Photograph; Screenshot" stays one cell
    private static readonly char[] OtherSpreadsheetSeparators = [';', '\t'];

    private static readonly CsvConfiguration Configuration = new(CultureInfo.InvariantCulture)
    {
        InjectionOptions = InjectionOptions.Escape,
        ShouldQuote = arguments =>
            ConfigurationFunctions.ShouldQuote(arguments)
            || arguments.Field?.IndexOfAny(OtherSpreadsheetSeparators) >= 0,
    };

    private readonly StringWriter _text = new();
    private readonly CsvHelper.CsvWriter _writer;

    public CsvText() => _writer = new CsvHelper.CsvWriter(_text, Configuration);

    public void AddRow(IEnumerable<string?> cells)
    {
        foreach (var cell in cells)
        {
            _writer.WriteField(cell);
        }

        _writer.NextRecord();
    }

    public override string ToString()
    {
        _writer.Flush();
        return _text.ToString();
    }
}
