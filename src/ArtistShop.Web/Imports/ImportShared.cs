using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Imports;

// what every CSV import has in common

// Column is null for a problem with the whole row or file; RowNumber 1 is the header row
public record ImportError(int RowNumber, string? Column, string Message);

public static class ImportLimits
{
    // the review posts the text back in a form field, and ASP.NET Core refuses a field over 4 MB
    public const int FileMaximumBytes = 1 * Units.BytesPerMebibyte;
}

public static class ImportNames
{
    // close to the database's case-insensitive, accent-sensitive comparison of names
    public static readonly StringComparer Comparer = StringComparer.InvariantCultureIgnoreCase;
}

public static class ImportLists
{
    // the names in one cell, trimmed, without blanks or repeats
    public static List<string> Split(string text, char separator) =>
        [.. text.Split(separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(ImportNames.Comparer)];
}

public enum ImportSkipReason : byte
{
    AlreadyExists = 1,
    RepeatedInFile = 2,
}

public record ImportSkippedRow(int RowNumber, string Name, ImportSkipReason Reason);

// what the import page needs from any plan
public interface IImportPlan
{
    IReadOnlyList<ImportError> Errors { get; }

    // how many things importing adds or changes
    int ChangeCount { get; }

    bool CanImport => Errors.Count == 0 && ChangeCount > 0;

    // a short text that changes whenever anything in the plan does, so a confirm can tell whether
    // the plan it rebuilt is the one the artist reviewed
    string Fingerprint();
}

public static class ImportColumns
{
    private const int HeaderRowNumber = 1;

    // where each header sits, for a file whose headers are all known in advance. Null when the
    // header row has a problem; every problem is added to errors
    public static Dictionary<string, int>? Find(
        CsvTable table,
        IReadOnlyList<string> knownHeaders,
        IReadOnlyList<string> requiredHeaders,
        List<ImportError> errors
    )
    {
        var errorCountBefore = errors.Count;
        var columns = new Dictionary<string, int>(ImportNames.Comparer);

        for (var index = 0; index < table.Headers.Count; index += 1)
        {
            var header = table.Headers[index];

            // spreadsheets often export empty columns past the data
            if (header.Length == 0)
            {
                if (table.Rows.Any(row => row.Cells[index].Length > 0))
                {
                    errors.Add(new ImportError(HeaderRowNumber, null, $"Column {index + 1} has values but no header."));
                }
            }
            else if (!knownHeaders.Contains(header, ImportNames.Comparer))
            {
                errors.Add(new ImportError(HeaderRowNumber, header, $"This isn't a column the import knows. Use {string.Join(", ", knownHeaders)}."));
            }
            else if (!columns.TryAdd(header, index))
            {
                errors.Add(new ImportError(HeaderRowNumber, header, "This header appears more than once."));
            }
        }

        foreach (var header in requiredHeaders.Where(header => !columns.ContainsKey(header)))
        {
            errors.Add(new ImportError(HeaderRowNumber, null, $"The file needs a \"{header}\" column."));
        }

        return errors.Count > errorCountBefore ? null : columns;
    }
}

// the one rule for the list separator field on every import form. Constants, since attributes need them
public static class ImportListSeparator
{
    public const string Pattern = """^[^\s"]$""";
    public const string Message = "Use a single character other than a quote or a space.";
}

public abstract record ImportFileText
{
    public sealed record Read(string Text) : ImportFileText;

    public sealed record Refused(string Problem) : ImportFileText;

    public static async Task<ImportFileText> ReadAsync(IFormFile file)
    {
        if (file.Length > ImportLimits.FileMaximumBytes)
        {
            return new Refused($"The file is over {ImportLimits.FileMaximumBytes / Units.BytesPerMebibyte} MB. Split it into smaller files.");
        }

        await using var stream = file.OpenReadStream();

        return await Utf8Text.TryReadAsync(stream) is string text
            ? new Read(text)
            : new Refused("The file isn't UTF-8 text. In your spreadsheet, save it as \"CSV UTF-8\".");
    }
}
