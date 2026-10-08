using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Imports;

public record WorkImportAddition(int RowNumber, WorkCatalogAddition Addition);

public enum WorkImportSkipReason : byte
{
    AlreadyInCatalog = 1,
    TitleRepeatedInFile = 2,

    // longer than a slug can be and still take a number, should another work here have it
    SlugTooLong = 3,
}

public record WorkImportSkippedRow(int RowNumber, string Title, WorkImportSkipReason Reason);

// a collection the file names that doesn't exist yet. RowCount is how many rows would join it, which
// is what gives a typo away: the real collection has twelve rows and the misspelling has one
public record WorkImportNewCollection(string Name, int RowCount);

public record WorkImportPlan(
    IReadOnlyList<WorkImportAddition> Additions,
    IReadOnlyList<WorkImportSkippedRow> SkippedRows,
    IReadOnlyList<ImportError> Errors,
    IReadOnlyList<WorkImportNewCollection> NewCollections,
    // names close enough to another collection to be worth a second look; they don't stop the import
    IReadOnlyList<CollectionNameLikeness> CollectionNameWarnings
)
{
    public static WorkImportPlan WithErrors(IReadOnlyList<ImportError> errors) =>
        new([], [], errors, [], []);

    public bool CanImport => Errors.Count == 0 && Additions.Count > 0;

    // a short text that changes whenever anything in the plan does, so a confirm can tell whether
    // the plan it rebuilt is the one the artist reviewed
    public string Fingerprint() => ImportPlanFingerprint.Of(this);
}
