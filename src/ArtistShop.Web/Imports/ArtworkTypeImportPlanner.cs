using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Imports;

public static class ArtworkTypeImportHeaders
{
    public const string ArtworkType = "artworkType";
    public const string Fields = "fields";

    public static readonly IReadOnlyList<string> All = [ArtworkType, Fields];
}

public record ArtworkTypeImportAddition(int RowNumber, ArtworkTypeName Name, IReadOnlyList<ArtworkField> Fields);

public record ArtworkTypeImportPlan(
    IReadOnlyList<ArtworkTypeImportAddition> Additions,
    IReadOnlyList<ImportSkippedRow> SkippedRows,
    IReadOnlyList<ImportError> Errors
) : IImportPlan
{
    public static ArtworkTypeImportPlan WithErrors(IReadOnlyList<ImportError> errors) => new([], [], errors);

    public int ChangeCount => Additions.Count;

    public string Fingerprint() => ImportPlanFingerprint.Of(this);
}

// Turns a CSV of artwork types into the types importing it would add, without touching the database.
// It only adds: a type that already exists is skipped, fields and all
public static class ArtworkTypeImportPlanner
{
    public static ArtworkTypeImportPlan Plan(string csvText, char listSeparator, CatalogSetupSnapshot snapshot)
    {
        CsvTable table;

        try
        {
            table = CsvTable.Parse(csvText);
        }
        catch (MalformedCsvException exception)
        {
            return ArtworkTypeImportPlan.WithErrors([new ImportError(exception.RowNumber, null, exception.Problem)]);
        }

        var errors = new List<ImportError>();
        var columns = ImportColumns.Find(table, ArtworkTypeImportHeaders.All, [ArtworkTypeImportHeaders.ArtworkType], errors);

        if (columns is null)
        {
            return ArtworkTypeImportPlan.WithErrors(errors);
        }

        var nameColumn = columns[ArtworkTypeImportHeaders.ArtworkType];
        int? fieldsColumn = columns.TryGetValue(ArtworkTypeImportHeaders.Fields, out var index) ? index : null;
        var existingNames = snapshot.Types.Select(type => type.Name.Value).ToHashSet(ImportNames.Comparer);
        var nameCounts = table.Rows
            .Select(row => row.Cells[nameColumn])
            .Where(name => name.Length > 0)
            .CountBy(name => name, ImportNames.Comparer)
            .ToDictionary(ImportNames.Comparer);

        var additions = new List<ArtworkTypeImportAddition>();
        var skippedRows = new List<ImportSkippedRow>();

        foreach (var row in table.Rows)
        {
            var name = row.Cells[nameColumn];

            void AddError(string column, string message) => errors.Add(new ImportError(row.RowNumber, column, message));

            if (name.Length == 0)
            {
                AddError(ArtworkTypeImportHeaders.ArtworkType, "Every row needs an artwork type's name.");
                continue;
            }

            if (nameCounts[name] > 1)
            {
                skippedRows.Add(new ImportSkippedRow(row.RowNumber, name, ImportSkipReason.RepeatedInFile));
                continue;
            }

            if (existingNames.Contains(name))
            {
                skippedRows.Add(new ImportSkippedRow(row.RowNumber, name, ImportSkipReason.AlreadyExists));
                continue;
            }

            var errorCountBefore = errors.Count;

            if (name.Length > ArtistShopLimits.ArtworkTypeNameMaximumLength)
            {
                AddError(ArtworkTypeImportHeaders.ArtworkType, $"Names can be at most {ArtistShopLimits.ArtworkTypeNameMaximumLength} characters.");
            }

            var fields = new List<ArtworkField>();
            var fieldNames = fieldsColumn is int column ? ImportLists.Split(row.Cells[column], listSeparator) : [];

            foreach (var fieldName in fieldNames)
            {
                var definition = snapshot.Fields.FirstOrDefault(definition => ImportNames.Comparer.Equals(definition.Name, fieldName));

                if (definition is null)
                {
                    var known = string.Join(", ", snapshot.Fields.Select(definition => definition.Name));
                    AddError(ArtworkTypeImportHeaders.Fields, $"\"{fieldName}\" isn't a field. The fields are {known}.");
                }
                else
                {
                    fields.Add(definition.Field);
                }
            }

            // the type form makes the same check: depth means nothing without height and width
            foreach (var definition in snapshot.Fields.Where(definition => definition.HasRequirement && fields.Contains(definition.Field)))
            {
                if (!fields.Contains(definition.RequiredField))
                {
                    var required = snapshot.Fields.First(other => other.Field == definition.RequiredField);
                    AddError(ArtworkTypeImportHeaders.Fields, $"\"{definition.Name}\" needs \"{required.Name}\" too.");
                }
            }

            if (errors.Count == errorCountBefore)
            {
                additions.Add(new ArtworkTypeImportAddition(row.RowNumber, new ArtworkTypeName(name), fields));
            }
        }

        return new ArtworkTypeImportPlan(additions, skippedRows, errors);
    }

    // one type at a time, so a failure part way leaves the ones before it; importing the file again
    // skips them. Throws NameAlreadyInUseException when a type was added since the review
    public static async Task ApplyAsync(ArtworkTypeImportPlan plan, ArtworkTypeRepository artworkTypeRepository)
    {
        foreach (var addition in plan.Additions)
        {
            await artworkTypeRepository.AddAsync(addition.Name, addition.Fields);
        }
    }
}
