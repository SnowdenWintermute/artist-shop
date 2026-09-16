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

public record ArtworkImportPlan(
    IReadOnlyList<ArtworkImportAddition> Additions,
    IReadOnlyList<ArtworkImportSkippedRow> SkippedRows,
    IReadOnlyList<ArtworkImportError> Errors
)
{
    public static ArtworkImportPlan WithErrors(IReadOnlyList<ArtworkImportError> errors) =>
        new([], [], errors);

    public bool CanImport => Errors.Count == 0 && Additions.Count > 0;

    // a short text that changes whenever anything in the plan does, so a confirm can tell whether
    // the plan it rebuilt is the one the artist reviewed
    public string Fingerprint() =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this)));
}
