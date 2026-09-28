namespace ArtistShop.Web.Imports;

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

// with CsvText, the only files that know which CSV library we use
public class CsvTable
{
    // what CsvText puts a ' in front of, so a spreadsheet doesn't run the cell as a formula. The
    // import takes it off again. \n too, since \r becomes \n before parsing
    private static readonly char[] EscapedFormulaStarts =
    [
        .. new CsvConfiguration(CultureInfo.InvariantCulture).InjectionCharacters,
        '\n',
    ];

    private CsvTable(IReadOnlyList<string> headers, IReadOnlyList<CsvRow> rows)
    {
        Headers = headers;
        Rows = rows;
    }

    // trimmed, in file order, duplicates kept so the caller can report them
    public IReadOnlyList<string> Headers { get; }

    // blank rows are left out; every row has exactly one cell per header
    public IReadOnlyList<CsvRow> Rows { get; }

    // line breaks inside cells come back as \n whatever the file used. A browser posting the text back
    // in a form turns them into \r\n, so without this the same file would read differently twice
    public static CsvTable Parse(string text)
    {
        text = text.ReplaceLineEndings("\n");

        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            // guessing could read a semicolon file as valid
            DetectDelimiter = false,
            // kept, so Row counts them as a spreadsheet counts its rows
            IgnoreBlankLines = false,
            BadDataFound = arguments =>
            {
                // a quote inside a cell that isn't quoted, like 12" x 16", is read as it is. A cell that
                // starts with one and doesn't end cleanly is broken
                if (arguments.Field.StartsWith('"'))
                {
                    throw new MalformedCsvException(
                        arguments.Context.Parser?.Row ?? throw new InvalidOperationException("A parser's bad data has no parser."),
                        "isn't valid CSV, often a quote that is never closed"
                    );
                }
            },
        };

        using var parser = new CsvParser(new StringReader(text), configuration);

        if (!parser.Read())
        {
            return new CsvTable([], []);
        }

        List<string> headers = [.. Cells(parser)];
        var rows = new List<CsvRow>();

        while (parser.Read())
        {
            // Row counts the header as 1 and a cell's line breaks not at all, as a spreadsheet does
            var rowNumber = parser.Row;
            var cells = Cells(parser);

            if (cells.Skip(headers.Count).Any(cell => cell.Length > 0))
            {
                throw new MalformedCsvException(rowNumber, "has more cells than the header row has names");
            }

            if (cells.All(cell => cell.Length == 0))
            {
                continue;
            }

            // a short row's missing cells are blank
            var missingCellCount = Math.Max(0, headers.Count - cells.Count);
            rows.Add(new CsvRow(rowNumber, [.. cells.Take(headers.Count), .. Enumerable.Repeat("", missingCellCount)]));
        }

        return new CsvTable(headers, rows);
    }

    private static List<string> Cells(CsvParser parser) =>
        [.. (parser.Record ?? []).Select(cell => WithoutFormulaEscape(cell).Trim())];

    private static string WithoutFormulaEscape(string cell) =>
        cell.Length > 1 && cell[0] == '\'' && EscapedFormulaStarts.Contains(cell[1]) ? cell[1..] : cell;
}

// RowNumber is the row a spreadsheet shows, counting the header as row 1
public record CsvRow(int RowNumber, IReadOnlyList<string> Cells);

public class MalformedCsvException(int rowNumber, string problem) : Exception($"Row {rowNumber} {problem}.")
{
    public int RowNumber { get; } = rowNumber;

    // the message without the row number, for callers that show the row separately
    public string Problem { get; } = problem;
}
