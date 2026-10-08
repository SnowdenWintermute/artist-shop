using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Imports;

public record VocabularyColumn(int Index, VocabularyWithTerms Vocabulary);

// where each known header sits in the file; null when the file doesn't have it
public record WorkImportColumns(
    int Title,
    int? Slug,
    int? Description,
    int? DateCreated,
    int? Height,
    int? Width,
    int? Depth,
    int? Duration,
    int? Collections,
    int? Price,
    int? Sold,
    int? EditionSize,
    int? Stock,
    IReadOnlyList<VocabularyColumn> Vocabularies
)
{
    private const int HeaderRowNumber = 1;

    // null when the header row has a problem; every problem is added to errors
    public static WorkImportColumns? Find(
        CsvTable table,
        WorkImportSettings settings,
        WorkImportCatalogSnapshot snapshot,
        List<ImportError> errors
    )
    {
        var errorCountBefore = errors.Count;
        var knownColumns = new Dictionary<string, int>(ImportNames.Comparer);
        var vocabularyColumns = new List<VocabularyColumn>();
        var seenHeaders = new HashSet<string>(ImportNames.Comparer);
        var typeName = snapshot.WorkType.Name.Value;

        void AddError(string? column, string message) =>
            errors.Add(new ImportError(HeaderRowNumber, column, message));

        for (var index = 0; index < table.Headers.Count; index += 1)
        {
            var header = table.Headers[index];

            // spreadsheets often export empty columns past the data
            if (header.Length == 0)
            {
                if (table.Rows.Any(row => row.Cells[index].Length > 0))
                {
                    AddError(null, $"Column {index + 1} has values but no header.");
                }

                continue;
            }

            if (!seenHeaders.Add(header))
            {
                AddError(header, "This header appears more than once.");
                continue;
            }

            var isPrefixed = header.StartsWith(WorkImportHeaders.VocabularyPrefix, StringComparison.OrdinalIgnoreCase);

            if (!isPrefixed && WorkImportHeaders.All.Contains(header))
            {
                knownColumns[header] = index;
                continue;
            }

            var vocabularyName = isPrefixed ? header[WorkImportHeaders.VocabularyPrefix.Length..].Trim() : header;
            var vocabulary = snapshot.AllVocabularies.FirstOrDefault(vocabulary =>
                ImportNames.Comparer.Equals(vocabulary.Name.Value, vocabularyName)
            );

            if (vocabulary is null)
            {
                AddError(
                    header,
                    isPrefixed
                        ? $"There's no vocabulary called \"{vocabularyName}\"."
                        : "This isn't a column the import knows, or the name of a vocabulary."
                );
            }
            else if (vocabularyColumns.Any(column => column.Vocabulary.Id == vocabulary.Id))
            {
                // "Medium" and "vocabulary:Medium" are different headers for the same vocabulary
                AddError(header, "This vocabulary already has a column.");
            }
            else if (snapshot.TypeVocabularies.FirstOrDefault(typeVocabulary => typeVocabulary.Id == vocabulary.Id) is { } typeVocabulary)
            {
                vocabularyColumns.Add(new VocabularyColumn(index, typeVocabulary));
            }
            else
            {
                AddError(header, $"This vocabulary doesn't apply to {typeName}. Choose where it applies on the vocabulary's page.");
            }
        }

        int? ColumnOf(string header) => knownColumns.TryGetValue(header, out var index) ? index : null;

        void RequireField(string header, WorkField field)
        {
            if (ColumnOf(header) is not null && !snapshot.WorkType.Fields.Contains(field))
            {
                AddError(header, $"{typeName} doesn't have this field. Switch it on for the type, or remove the column.");
            }
        }

        RequireField(WorkImportHeaders.DateCreated, WorkField.DateCreated);
        RequireField(WorkImportHeaders.Height, WorkField.HeightAndWidth);
        RequireField(WorkImportHeaders.Width, WorkField.HeightAndWidth);
        RequireField(WorkImportHeaders.Depth, WorkField.Depth);
        RequireField(WorkImportHeaders.Duration, WorkField.Duration);

        if ((ColumnOf(WorkImportHeaders.Height) is null) != (ColumnOf(WorkImportHeaders.Width) is null))
        {
            AddError(null, "Height and width go together: include both columns, or neither.");
        }

        if (ColumnOf(WorkImportHeaders.Depth) is not null && ColumnOf(WorkImportHeaders.Height) is null)
        {
            AddError(WorkImportHeaders.Depth, "A depth column needs height and width columns.");
        }

        if (settings.IsOneOfAKind)
        {
            foreach (var header in (string[])[WorkImportHeaders.EditionSize, WorkImportHeaders.Stock])
            {
                if (ColumnOf(header) is not null)
                {
                    AddError(header, "One-of-a-kind products don't use this column. Remove it, or uncheck one of a kind.");
                }
            }
        }
        else
        {
            if (ColumnOf(WorkImportHeaders.Sold) is not null)
            {
                AddError(WorkImportHeaders.Sold, "Only one-of-a-kind products use this column. Give each row a stock instead.");
            }

            foreach (var header in (string[])[WorkImportHeaders.EditionSize, WorkImportHeaders.Stock])
            {
                if (ColumnOf(header) is null)
                {
                    AddError(null, $"Products that aren't one of a kind need a \"{header}\" column.");
                }
            }
        }

        if (!snapshot.ProductTypes.Any(productType => productType.Id == settings.ProductTypeId))
        {
            AddError(null, "The chosen product type no longer exists.");
        }

        if (ColumnOf(WorkImportHeaders.Title) is not int titleColumn)
        {
            AddError(null, $"The file needs a \"{WorkImportHeaders.Title}\" column.");
            return null;
        }

        if (errors.Count > errorCountBefore)
        {
            return null;
        }

        return new WorkImportColumns(
            titleColumn,
            ColumnOf(WorkImportHeaders.Slug),
            ColumnOf(WorkImportHeaders.Description),
            ColumnOf(WorkImportHeaders.DateCreated),
            ColumnOf(WorkImportHeaders.Height),
            ColumnOf(WorkImportHeaders.Width),
            ColumnOf(WorkImportHeaders.Depth),
            ColumnOf(WorkImportHeaders.Duration),
            ColumnOf(WorkImportHeaders.Collections),
            ColumnOf(WorkImportHeaders.Price),
            ColumnOf(WorkImportHeaders.Sold),
            ColumnOf(WorkImportHeaders.EditionSize),
            ColumnOf(WorkImportHeaders.Stock),
            vocabularyColumns
        );
    }
}
