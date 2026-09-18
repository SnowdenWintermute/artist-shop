using System.Security.Cryptography;
using System.Text.Json;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Imports;

public record ArtworkImportAddition(int RowNumber, ArtworkCatalogAddition Addition);

public enum ArtworkImportSkipReason : byte
{
    AlreadyInCatalog = 1,
    TitleRepeatedInFile = 2,
}

public record ArtworkImportSkippedRow(int RowNumber, string Title, ArtworkImportSkipReason Reason);

// Column is null for a problem with the whole row or file; RowNumber 1 is the header row
public record ArtworkImportError(int RowNumber, string? Column, string Message);

// a series the file names that doesn't exist yet. RowCount is how many rows would join it, which
// is what gives a typo away: the real series has twelve rows and the misspelling has one
public record ArtworkImportNewSeries(string Name, int RowCount);

public record ArtworkImportPlan(
    IReadOnlyList<ArtworkImportAddition> Additions,
    IReadOnlyList<ArtworkImportSkippedRow> SkippedRows,
    IReadOnlyList<ArtworkImportError> Errors,
    IReadOnlyList<ArtworkImportNewSeries> NewSeries,
    // names close enough to another series to be worth a second look; they don't stop the import
    IReadOnlyList<SeriesNameLikeness> SeriesNameWarnings
)
{
    public static ArtworkImportPlan WithErrors(IReadOnlyList<ArtworkImportError> errors) =>
        new([], [], errors, [], []);

    public bool CanImport => Errors.Count == 0 && Additions.Count > 0;

    // a short text that changes whenever anything in the plan does, so a confirm can tell whether
    // the plan it rebuilt is the one the artist reviewed
    public string Fingerprint() =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this)));
}
