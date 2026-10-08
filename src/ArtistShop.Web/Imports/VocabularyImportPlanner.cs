using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Imports;

public static class VocabularyImportHeaders
{
    public const string Vocabulary = "vocabulary";
    public const string WorkTypes = "workTypes";
    public const string Terms = "terms";
    // yes or no; blank is no
    public const string MutuallyExclusive = "mutuallyExclusive";

    public static readonly IReadOnlyList<string> All = [Vocabulary, WorkTypes, Terms, MutuallyExclusive];

    public const string Yes = "yes";
    public const string No = "no";
}

// What importing a row adds. ExistingId is null for a new vocabulary; for one that exists, Name and
// IsMutuallyExclusive are its own, and the added types and terms are only the ones it doesn't have yet
public record VocabularyImportChange(
    int RowNumber,
    VocabularyName Name,
    VocabularyId? ExistingId,
    bool IsMutuallyExclusive,
    IReadOnlyList<WorkType> ExistingWorkTypes,
    IReadOnlyList<WorkType> AddedWorkTypes,
    IReadOnlyList<VocabularyTermName> AddedTerms
);

public record VocabularyImportPlan(
    IReadOnlyList<VocabularyImportChange> Changes,
    IReadOnlyList<ImportSkippedRow> SkippedRows,
    IReadOnlyList<ImportError> Errors
) : IImportPlan
{
    public static VocabularyImportPlan WithErrors(IReadOnlyList<ImportError> errors) => new([], [], errors);

    public int ChangeCount => Changes.Count;

    public string Fingerprint() => ImportPlanFingerprint.Of(this);
}

// Turns a CSV of vocabularies into what importing it would add, without touching the database. It
// only adds: new vocabularies, and the types and terms an existing one is missing. Nothing is removed
// or renamed
public static class VocabularyImportPlanner
{
    public static VocabularyImportPlan Plan(string csvText, char listSeparator, CatalogSetupSnapshot snapshot)
    {
        CsvTable table;

        try
        {
            table = CsvTable.Parse(csvText);
        }
        catch (MalformedCsvException exception)
        {
            return VocabularyImportPlan.WithErrors([new ImportError(exception.RowNumber, null, exception.Problem)]);
        }

        var errors = new List<ImportError>();
        var columns = ImportColumns.Find(table, VocabularyImportHeaders.All, [VocabularyImportHeaders.Vocabulary], errors);

        if (columns is null)
        {
            return VocabularyImportPlan.WithErrors(errors);
        }

        var nameColumn = columns[VocabularyImportHeaders.Vocabulary];
        var nameCounts = table.Rows
            .Select(row => row.Cells[nameColumn])
            .Where(name => name.Length > 0)
            .CountBy(name => name, ImportNames.Comparer)
            .ToDictionary(ImportNames.Comparer);

        List<string> ListIn(CsvRow row, string header) =>
            columns.TryGetValue(header, out var column) ? ImportLists.Split(row.Cells[column], listSeparator) : [];

        string MutuallyExclusiveIn(CsvRow row) =>
            columns.TryGetValue(VocabularyImportHeaders.MutuallyExclusive, out var column) ? row.Cells[column].Trim() : "";

        var changes = new List<VocabularyImportChange>();
        var skippedRows = new List<ImportSkippedRow>();

        foreach (var row in table.Rows)
        {
            var name = row.Cells[nameColumn];

            void AddError(string column, string message) => errors.Add(new ImportError(row.RowNumber, column, message));

            if (name.Length == 0)
            {
                AddError(VocabularyImportHeaders.Vocabulary, "Every row needs a vocabulary's name.");
                continue;
            }

            if (nameCounts[name] > 1)
            {
                skippedRows.Add(new ImportSkippedRow(row.RowNumber, name, ImportSkipReason.RepeatedInFile));
                continue;
            }

            var errorCountBefore = errors.Count;

            if (name.Length > ArtistShopLimits.VocabularyNameMaximumLength)
            {
                AddError(VocabularyImportHeaders.Vocabulary, $"Names can be at most {ArtistShopLimits.VocabularyNameMaximumLength} characters.");
            }

            var types = new List<WorkType>();

            foreach (var typeName in ListIn(row, VocabularyImportHeaders.WorkTypes))
            {
                var type = snapshot.Types.FirstOrDefault(type => ImportNames.Comparer.Equals(type.Name.Value, typeName));

                if (type is null)
                {
                    AddError(VocabularyImportHeaders.WorkTypes, $"\"{typeName}\" isn't a work type here. Import the work types first.");
                }
                else
                {
                    types.Add(new WorkType(type.Id, type.Name));
                }
            }

            var terms = ListIn(row, VocabularyImportHeaders.Terms);

            foreach (var term in terms.Where(term => term.Length > ArtistShopLimits.VocabularyTermNameMaximumLength))
            {
                AddError(VocabularyImportHeaders.Terms, $"\"{term}\" is longer than {ArtistShopLimits.VocabularyTermNameMaximumLength} characters.");
            }

            var mutuallyExclusiveText = MutuallyExclusiveIn(row);
            var isMutuallyExclusive = StringComparer.OrdinalIgnoreCase.Equals(mutuallyExclusiveText, VocabularyImportHeaders.Yes);

            if (!isMutuallyExclusive && mutuallyExclusiveText.Length > 0 && !StringComparer.OrdinalIgnoreCase.Equals(mutuallyExclusiveText, VocabularyImportHeaders.No))
            {
                AddError(VocabularyImportHeaders.MutuallyExclusive, $"Write {VocabularyImportHeaders.Yes} or {VocabularyImportHeaders.No}, or leave it blank for {VocabularyImportHeaders.No}.");
            }

            var existing = snapshot.Vocabularies.FirstOrDefault(vocabulary => ImportNames.Comparer.Equals(vocabulary.Name.Value, name));

            // the import only adds, and making a vocabulary mutually exclusive can take terms off works
            if (existing is not null && existing.IsMutuallyExclusive != isMutuallyExclusive)
            {
                AddError(
                    VocabularyImportHeaders.MutuallyExclusive,
                    existing.IsMutuallyExclusive
                        ? $"{existing.Name.Value} is already here and is mutually exclusive. Write {VocabularyImportHeaders.Yes}, or change it on the vocabulary's page."
                        : $"{existing.Name.Value} is already here and isn't mutually exclusive. Write {VocabularyImportHeaders.No}, or change it on the vocabulary's page."
                );
            }

            if (errors.Count > errorCountBefore)
            {
                continue;
            }

            if (existing is null)
            {
                changes.Add(new VocabularyImportChange(
                    row.RowNumber,
                    new VocabularyName(name),
                    ExistingId: null,
                    isMutuallyExclusive,
                    ExistingWorkTypes: [],
                    types,
                    [.. terms.Select(term => new VocabularyTermName(term))]
                ));
                continue;
            }

            var addedTypes = types.Where(type => !existing.WorkTypeIds.Contains(type.Id)).ToList();
            var addedTerms = terms
                .Where(term => !existing.Terms.Any(existingTerm => ImportNames.Comparer.Equals(existingTerm.Name.Value, term)))
                .Select(term => new VocabularyTermName(term))
                .ToList();

            if (addedTypes.Count == 0 && addedTerms.Count == 0)
            {
                skippedRows.Add(new ImportSkippedRow(row.RowNumber, name, ImportSkipReason.AlreadyExists));
                continue;
            }

            var existingTypes = snapshot.Types
                .Where(type => existing.WorkTypeIds.Contains(type.Id))
                .Select(type => new WorkType(type.Id, type.Name))
                .ToList();

            changes.Add(new VocabularyImportChange(row.RowNumber, existing.Name, existing.Id, existing.IsMutuallyExclusive, existingTypes, addedTypes, addedTerms));
        }

        return new VocabularyImportPlan(changes, skippedRows, errors);
    }

    // One vocabulary at a time, so a failure part way leaves the ones before it; importing the file
    // again adds only what's still missing. Throws NameAlreadyInUseException or
    // ChangedSincePageLoadException when the catalog changed since the review
    public static async Task ApplyAsync(
        VocabularyImportPlan plan,
        VocabularyRepository vocabularyRepository,
        VocabularyTermRepository vocabularyTermRepository
    )
    {
        foreach (var change in plan.Changes)
        {
            var addedTypeIds = change.AddedWorkTypes.Select(type => type.Id);
            VocabularyId id;

            if (change.ExistingId is { } existingId)
            {
                id = existingId;

                if (change.AddedWorkTypes.Count > 0)
                {
                    await vocabularyRepository.UpdateAsync(id, change.Name, change.IsMutuallyExclusive, [.. change.ExistingWorkTypes.Select(type => type.Id), .. addedTypeIds]);
                }
            }
            else
            {
                id = await vocabularyRepository.AddAsync(change.Name, change.IsMutuallyExclusive, addedTypeIds);
            }

            foreach (var term in change.AddedTerms)
            {
                await vocabularyTermRepository.AddAsync(id, term);
            }
        }
    }
}
