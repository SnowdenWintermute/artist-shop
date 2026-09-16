namespace ArtistShop.Web.Imports;

using Sylvan.Data.Csv;

// the only file that knows which CSV library we use
public class CsvTable
{
    private CsvTable(IReadOnlyList<string> headers, IReadOnlyList<CsvRow> rows)
    {
        Headers = headers;
        Rows = rows;
    }

    // trimmed, in file order, duplicates kept so the caller can report them
    public IReadOnlyList<string> Headers { get; }

    // blank rows are left out; every row has exactly one cell per header
    public IReadOnlyList<CsvRow> Rows { get; }

    public static CsvTable Parse(string text)
    {
        // Sylvan throws on a file with no header row
        if (string.IsNullOrWhiteSpace(text))
        {
            return new CsvTable([], []);
        }

        try
        {
            // setting the delimiter turns off Sylvan's guessing, which could read a semicolon file as valid
            using var reader = CsvDataReader.Create(new StringReader(text), new CsvDataReaderOptions { Delimiter = ',' });

            List<string> headers =
            [
                .. Enumerable.Range(0, reader.FieldCount).Select(index => reader.GetName(index).Trim()),
            ];
            var rows = new List<CsvRow>();

            while (reader.Read())
            {
                var rowNumber = SpreadsheetRowNumber(reader.RowNumber);
                List<string> cells =
                [
                    .. Enumerable.Range(0, reader.RowFieldCount).Select(index => reader.GetString(index).Trim()),
                ];

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
        catch (CsvFormatException exception)
        {
            throw new MalformedCsvException(SpreadsheetRowNumber(exception.RowNumber), "isn't valid CSV, often a quote that is never closed", exception);
        }
    }

    // Sylvan counts data rows from 1, with blank lines counted and a cell's line breaks not, so only
    // the header row is missing
    private static int SpreadsheetRowNumber(int sylvanRowNumber) => sylvanRowNumber + 1;
}

// RowNumber is the row a spreadsheet shows, counting the header as row 1
public record CsvRow(int RowNumber, IReadOnlyList<string> Cells);

public class MalformedCsvException(int rowNumber, string problem, Exception? innerException = null)
    : Exception($"Row {rowNumber} {problem}.", innerException)
{
    public int RowNumber { get; } = rowNumber;

    // the message without the row number, for callers that show the row separately
    public string Problem { get; } = problem;
}
