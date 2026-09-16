using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Imports;

public record VocabularyColumn(int Index, VocabularyWithTerms Vocabulary);

// where each known header sits in the file; null when the file doesn't have it
public record ArtworkImportColumns(
    int Title,
    int? Description,
    int? DateCreated,
    int? Height,
    int? Width,
    int? Depth,
    int? Duration,
    int? Series,
    int? Price,
    int? Sold,
    int? EditionSize,
    int? Stock,
    IReadOnlyList<VocabularyColumn> Vocabularies
)
{
    private const int HeaderRowNumber = 1;

    // null when the header row has a problem; every problem is added to errors
    public static ArtworkImportColumns? Find(
        CsvTable table,
        ArtworkImportSettings settings,
        ArtworkImportCatalogSnapshot snapshot,
        List<ArtworkImportError> errors
    )
    {
        var errorCountBefore = errors.Count;
        var knownColumns = new Dictionary<string, int>(ArtworkImportNames.Comparer);
        var vocabularyColumns = new List<VocabularyColumn>();
        var seenHeaders = new HashSet<string>(ArtworkImportNames.Comparer);
        var typeName = snapshot.ArtworkType.Name.Value;

        void AddError(string? column, string message) =>
            errors.Add(new ArtworkImportError(HeaderRowNumber, column, message));

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

            var isKnownHeader = ArtworkImportHeaders.All.Contains(header);
            var vocabulary = snapshot.AllVocabularies.FirstOrDefault(vocabulary =>
                ArtworkImportNames.Comparer.Equals(vocabulary.Name.Value, header)
            );

            if (isKnownHeader && vocabulary is not null)
            {
                AddError(header, "A vocabulary has the same name as this import column. Rename the vocabulary.");
            }
            else if (isKnownHeader)
            {
                knownColumns[header] = index;
            }
            else if (vocabulary is null)
            {
                AddError(header, "This isn't a column the import knows, or the name of a vocabulary.");
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

        void RequireField(string header, ArtworkField field)
        {
            if (ColumnOf(header) is not null && !snapshot.ArtworkType.Fields.Contains(field))
            {
                AddError(header, $"{typeName} doesn't have this field. Switch it on for the type, or remove the column.");
            }
        }

        RequireField(ArtworkImportHeaders.DateCreated, ArtworkField.DateCreated);
        RequireField(ArtworkImportHeaders.Height, ArtworkField.HeightAndWidth);
        RequireField(ArtworkImportHeaders.Width, ArtworkField.HeightAndWidth);
        RequireField(ArtworkImportHeaders.Depth, ArtworkField.Depth);
        RequireField(ArtworkImportHeaders.Duration, ArtworkField.Duration);

        if ((ColumnOf(ArtworkImportHeaders.Height) is null) != (ColumnOf(ArtworkImportHeaders.Width) is null))
        {
            AddError(null, "Height and width go together: include both columns, or neither.");
        }

        if (ColumnOf(ArtworkImportHeaders.Depth) is not null && ColumnOf(ArtworkImportHeaders.Height) is null)
        {
            AddError(ArtworkImportHeaders.Depth, "A depth column needs height and width columns.");
        }

        if (settings.IsOneOfAKind)
        {
            foreach (var header in (string[])[ArtworkImportHeaders.EditionSize, ArtworkImportHeaders.Stock])
            {
                if (ColumnOf(header) is not null)
                {
                    AddError(header, "One-of-a-kind products don't use this column. Remove it, or untick one of a kind.");
                }
            }
        }
        else
        {
            if (ColumnOf(ArtworkImportHeaders.Sold) is not null)
            {
                AddError(ArtworkImportHeaders.Sold, "Only one-of-a-kind products use this column. Give each row a stock instead.");
            }

            foreach (var header in (string[])[ArtworkImportHeaders.EditionSize, ArtworkImportHeaders.Stock])
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

        if (ColumnOf(ArtworkImportHeaders.Title) is not int titleColumn)
        {
            AddError(null, $"The file needs a \"{ArtworkImportHeaders.Title}\" column.");
            return null;
        }

        if (errors.Count > errorCountBefore)
        {
            return null;
        }

        return new ArtworkImportColumns(
            titleColumn,
            ColumnOf(ArtworkImportHeaders.Description),
            ColumnOf(ArtworkImportHeaders.DateCreated),
            ColumnOf(ArtworkImportHeaders.Height),
            ColumnOf(ArtworkImportHeaders.Width),
            ColumnOf(ArtworkImportHeaders.Depth),
            ColumnOf(ArtworkImportHeaders.Duration),
            ColumnOf(ArtworkImportHeaders.Series),
            ColumnOf(ArtworkImportHeaders.Price),
            ColumnOf(ArtworkImportHeaders.Sold),
            ColumnOf(ArtworkImportHeaders.EditionSize),
            ColumnOf(ArtworkImportHeaders.Stock),
            vocabularyColumns
        );
    }
}
