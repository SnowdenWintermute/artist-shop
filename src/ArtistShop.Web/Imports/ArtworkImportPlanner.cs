using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;

namespace ArtistShop.Web.Imports;

// turns a CSV into what importing it would do, without touching the database
public static class ArtworkImportPlanner
{
    public static ArtworkImportPlan Plan(
        string csvText,
        ArtworkImportSettings settings,
        ArtworkImportCatalogSnapshot snapshot
    )
    {
        CsvTable table;

        try
        {
            table = CsvTable.Parse(csvText);
        }
        catch (MalformedCsvException exception)
        {
            return ArtworkImportPlan.WithErrors([new ArtworkImportError(exception.RowNumber, null, exception.Problem)]);
        }

        var errors = new List<ArtworkImportError>();

        if (ArtworkImportColumns.Find(table, settings, snapshot, errors) is not { } columns)
        {
            return ArtworkImportPlan.WithErrors(errors);
        }

        var existingNames = new HashSet<string>(snapshot.ArtworkNames, ArtworkImportNames.Comparer);
        var titleCounts = table.Rows
            .Select(row => row.Cells[columns.Title])
            .Where(title => title.Length > 0)
            .CountBy(title => title, ArtworkImportNames.Comparer)
            .ToDictionary(ArtworkImportNames.Comparer);

        var additions = new List<ArtworkImportAddition>();
        var skippedRows = new List<ArtworkImportSkippedRow>();

        // skipped rows aren't checked: a file re-imported to add new rows shouldn't fail on old ones
        foreach (var row in table.Rows)
        {
            var reader = new ArtworkImportRowReader(row);
            var title = row.Cells[columns.Title];

            if (title.Length == 0)
            {
                reader.AddError(ArtworkImportHeaders.Title, "Every row needs a title.");
            }
            else if (titleCounts[title] > 1)
            {
                skippedRows.Add(new ArtworkImportSkippedRow(row.RowNumber, title, ArtworkImportSkipReason.TitleRepeatedInFile));
                continue;
            }
            else if (existingNames.Contains(title))
            {
                skippedRows.Add(new ArtworkImportSkippedRow(row.RowNumber, title, ArtworkImportSkipReason.AlreadyInCatalog));
                continue;
            }

            var addition = ReadAddition(reader, title, columns, settings, snapshot);
            errors.AddRange(reader.Errors);

            if (addition is not null && reader.Errors.Count == 0)
            {
                additions.Add(new ArtworkImportAddition(row.RowNumber, addition));
            }
        }

        var newSeriesRows = CollectNewSeriesRows(additions);
        var newSeries = newSeriesRows
            .OrderBy(entry => entry.Key, ArtworkImportNames.Comparer)
            .Select(entry => new ArtworkImportNewSeries(entry.Key, entry.Value.Count))
            .ToList();

        var checks = SeriesNameSimilarity.Check(
            [.. newSeries.Select(series => series.Name)],
            [.. snapshot.AllSeries.Select(series => series.Name.Value)]
        );

        foreach (var collision in checks.SlugCollisions)
        {
            errors.Add(
                new ArtworkImportError(
                    // the row is where the name first appears, so there is somewhere to go and fix it
                    newSeriesRows[collision.Name][0],
                    ArtworkImportHeaders.Series,
                    $"\"{collision.Name}\" and \"{collision.MatchedName}\" would have the same web address. Rename one of them."
                )
            );
        }

        return new ArtworkImportPlan(additions, skippedRows, errors, newSeries, checks.NearDuplicates);
    }

    // which rows would join each series the file creates. Only rows that will be imported count:
    // a skipped or broken row creates nothing
    private static Dictionary<string, List<int>> CollectNewSeriesRows(
        IReadOnlyList<ArtworkImportAddition> additions
    )
    {
        var rowNumbers = new Dictionary<string, List<int>>(ArtworkImportNames.Comparer);

        foreach (var addition in additions)
        {
            foreach (var name in addition.Addition.NewSeriesNames)
            {
                // the first spelling of a name is the one the series gets
                if (!rowNumbers.TryGetValue(name.Value, out var rows))
                {
                    rows = [];
                    rowNumbers[name.Value] = rows;
                }

                rows.Add(addition.RowNumber);
            }
        }

        return rowNumbers;
    }

    private static ArtworkCatalogAddition? ReadAddition(
        ArtworkImportRowReader reader,
        string title,
        ArtworkImportColumns columns,
        ArtworkImportSettings settings,
        ArtworkImportCatalogSnapshot snapshot
    )
    {
        if (title.Length > CatalogLimits.ArtworkNameMaximumLength)
        {
            reader.AddError(ArtworkImportHeaders.Title, $"Titles can be at most {CatalogLimits.ArtworkNameMaximumLength} characters.");
        }
        else if (title.Length > 0 && ArtworkSlug.FromName(title).Value.Length == 0)
        {
            reader.AddError(ArtworkImportHeaders.Title, "This title has no letters or numbers to build a web address from.");
        }

        var dateCreated = reader.Date(columns.DateCreated, ArtworkImportHeaders.DateCreated);
        var dimensions = ReadDimensions(reader, columns, settings.LengthUnit);
        var duration = reader.Duration(columns.Duration, ArtworkImportHeaders.Duration);
        var termIds = ReadTermIds(reader, columns, settings.ListSeparator);
        var series = ReadSeries(reader, columns, settings.ListSeparator, snapshot.AllSeries);
        var product = settings.IsOneOfAKind
            ? ReadOneOfAKindProduct(reader, columns, settings.ProductTypeId)
            : ReadEditionProduct(reader, columns, settings.ProductTypeId);

        if (reader.Errors.Count > 0)
        {
            return null;
        }

        return new ArtworkCatalogAddition(
            snapshot.ArtworkType.Id,
            new ArtworkName(title),
            ArtworkSlug.FromName(title),
            reader.Text(columns.Description),
            dateCreated,
            dimensions,
            duration,
            Images: [],
            MainImageIndex: 0,
            VocabularyTermIds: termIds,
            SeriesIds: series.ExistingIds,
            NewSeriesNames: series.NewNames,
            Products: product is null ? [] : [product]
        );
    }

    private static DimensionsCentimeters? ReadDimensions(
        ArtworkImportRowReader reader,
        ArtworkImportColumns columns,
        LengthUnit unit
    )
    {
        var height = reader.LengthInCentimeters(columns.Height, ArtworkImportHeaders.Height, unit);
        var width = reader.LengthInCentimeters(columns.Width, ArtworkImportHeaders.Width, unit);
        var depth = reader.LengthInCentimeters(columns.Depth, ArtworkImportHeaders.Depth, unit);

        // a cell that failed to read already has its own error
        var heightMissing = reader.Text(columns.Height) is null;
        var widthMissing = reader.Text(columns.Width) is null;

        if (heightMissing != widthMissing)
        {
            reader.AddError(heightMissing ? ArtworkImportHeaders.Height : ArtworkImportHeaders.Width, "Give both height and width, or neither.");
        }
        else if (heightMissing && reader.Text(columns.Depth) is not null)
        {
            reader.AddError(ArtworkImportHeaders.Depth, "A depth needs a height and width.");
        }

        return height is decimal knownHeight && width is decimal knownWidth
            ? new DimensionsCentimeters(new Dimensions(knownHeight, knownWidth, depth))
            : null;
    }

    private static List<VocabularyTermId> ReadTermIds(
        ArtworkImportRowReader reader,
        ArtworkImportColumns columns,
        char separator
    )
    {
        var termIds = new List<VocabularyTermId>();

        foreach (var (index, vocabulary) in columns.Vocabularies)
        {
            foreach (var name in reader.List(index, separator))
            {
                var term = vocabulary.Terms.FirstOrDefault(term => ArtworkImportNames.Comparer.Equals(term.Name.Value, name));

                if (term is null)
                {
                    reader.AddError(vocabulary.Name.Value, $"\"{name}\" isn't a term in {vocabulary.Name.Value}.");
                }
                else
                {
                    termIds.Add(term.Id);
                }
            }
        }

        return termIds;
    }

    private record RowSeries(List<SeriesId> ExistingIds, List<SeriesName> NewNames);

    // a name that isn't a series yet is one the import creates; the review lists them with how many
    // rows use each, so a typo stands out before it becomes a series of one
    private static RowSeries ReadSeries(
        ArtworkImportRowReader reader,
        ArtworkImportColumns columns,
        char separator,
        IReadOnlyList<Series> allSeries
    )
    {
        var existingIds = new List<SeriesId>();
        var newNames = new List<SeriesName>();

        foreach (var name in reader.List(columns.Series, separator))
        {
            var series = allSeries.FirstOrDefault(series => ArtworkImportNames.Comparer.Equals(series.Name.Value, name));

            if (series is not null)
            {
                existingIds.Add(series.Id);
            }
            else if (name.Length > CatalogLimits.SeriesNameMaximumLength)
            {
                reader.AddError(ArtworkImportHeaders.Series, $"Series names can be at most {CatalogLimits.SeriesNameMaximumLength} characters.");
            }
            else if (SeriesSlug.FromName(name).Value.Length == 0)
            {
                reader.AddError(ArtworkImportHeaders.Series, $"\"{name}\" has no letters or numbers to build a web address from.");
            }
            else
            {
                newNames.Add(new SeriesName(name));
            }
        }

        return new RowSeries(existingIds, newNames);
    }

    // edition size 1; sold means none left. A sold row may have no price
    private static ProductAddition? ReadOneOfAKindProduct(
        ArtworkImportRowReader reader,
        ArtworkImportColumns columns,
        ProductTypeId productTypeId
    )
    {
        var price = reader.Price(columns.Price, ArtworkImportHeaders.Price);
        var isSold = reader.Boolean(columns.Sold, ArtworkImportHeaders.Sold);

        if (!isSold && price is null)
        {
            return null;
        }

        return new ProductAddition(productTypeId, Label: null, price, EditionSize: 1, Stock: isSold ? 0 : 1);
    }

    // a blank edition size is an open edition
    private static ProductAddition? ReadEditionProduct(
        ArtworkImportRowReader reader,
        ArtworkImportColumns columns,
        ProductTypeId productTypeId
    )
    {
        var price = reader.Price(columns.Price, ArtworkImportHeaders.Price);
        var editionSize = reader.WholeNumber(columns.EditionSize, ArtworkImportHeaders.EditionSize, minimum: 1);
        var stock = reader.WholeNumber(columns.Stock, ArtworkImportHeaders.Stock, minimum: 0);

        var productCellsBlank = new[] { columns.Price, columns.EditionSize, columns.Stock }.All(column => reader.Text(column) is null);

        if (productCellsBlank)
        {
            return null;
        }

        if (reader.Text(columns.Stock) is null)
        {
            reader.AddError(ArtworkImportHeaders.Stock, "A row with a price or edition size needs a stock.");
            return null;
        }

        if (stock is not int knownStock)
        {
            return null;
        }

        if (knownStock > editionSize)
        {
            reader.AddError(ArtworkImportHeaders.Stock, "Stock can't be more than the edition size.");
        }

        if (price is null && knownStock > 0 && reader.Text(columns.Price) is null)
        {
            reader.AddError(ArtworkImportHeaders.Price, "A product with stock needs a price.");
        }

        return new ProductAddition(productTypeId, Label: null, price, editionSize, knownStock);
    }
}
