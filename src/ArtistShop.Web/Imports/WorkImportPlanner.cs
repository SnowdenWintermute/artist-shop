using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;

namespace ArtistShop.Web.Imports;

// turns a CSV into what importing it would do, without touching the database
public static class WorkImportPlanner
{
    public static WorkImportPlan Plan(
        string csvText,
        WorkImportSettings settings,
        WorkImportCatalogSnapshot snapshot
    )
    {
        CsvTable table;

        try
        {
            table = CsvTable.Parse(csvText);
        }
        catch (MalformedCsvException exception)
        {
            return WorkImportPlan.WithErrors([new ImportError(exception.RowNumber, null, exception.Problem)]);
        }

        var errors = new List<ImportError>();

        if (WorkImportColumns.Find(table, settings, snapshot, errors) is not { } columns)
        {
            return WorkImportPlan.WithErrors(errors);
        }

        string SlugOf(CsvRow row) => columns.Slug is int slugColumn ? row.Cells[slugColumn] : "";

        var existingNames = new HashSet<string>(snapshot.TypeWorks.Select(work => work.Title), ImportNames.Comparer);
        var rowsByTitle = table.Rows
            .Where(row => row.Cells[columns.Title].Length > 0)
            .GroupBy(row => row.Cells[columns.Title], ImportNames.Comparer)
            .ToDictionary(group => group.Key, group => group.ToList(), ImportNames.Comparer);

        // A title on several rows is imported only when each row has a slug of its own, as the
        // catalog download writes for two works of a type with one title. Such a row is here
        // already when a work has its title and slug; any other title, when one has its title
        bool IsToldApart(List<CsvRow> sameTitle) =>
            sameTitle.All(row => SlugOf(row).Length > 0)
            && sameTitle.Select(SlugOf).Distinct(StringComparer.Ordinal).Count() == sameTitle.Count;

        bool IsHere(string title, string slug, bool isToldApart) =>
            isToldApart
                ? snapshot.TypeWorks.Any(work => ImportNames.Comparer.Equals(work.Title, title) && work.Slug == slug)
                : existingNames.Contains(title);

        var additions = new List<WorkImportAddition>();
        var skippedRows = new List<WorkImportSkippedRow>();

        // skipped rows aren't checked: a file re-imported to add new rows shouldn't fail on old ones
        foreach (var row in table.Rows)
        {
            var reader = new WorkImportRowReader(row);
            var title = row.Cells[columns.Title];
            var slug = SlugOf(row);

            if (title.Length == 0)
            {
                reader.AddError(WorkImportHeaders.Title, "Every row needs a title.");
            }
            else if (rowsByTitle[title] is { Count: > 1 } sameTitle && !IsToldApart(sameTitle))
            {
                skippedRows.Add(new WorkImportSkippedRow(row.RowNumber, title, WorkImportSkipReason.TitleRepeatedInFile));
                continue;
            }
            else if (IsHere(title, slug, isToldApart: rowsByTitle[title].Count > 1))
            {
                skippedRows.Add(new WorkImportSkippedRow(row.RowNumber, title, WorkImportSkipReason.AlreadyInCatalog));
                continue;
            }
            // Skipped rather than given another address: the work would come over under a slug
            // other than its own, and its images and post links would quietly miss it. One longer
            // still isn't a slug at all, which ReadAddition reports
            else if (slug.Length is > ArtistShopLimits.BaseSlugMaximumLength and <= ArtistShopLimits.SlugMaximumLength)
            {
                skippedRows.Add(new WorkImportSkippedRow(row.RowNumber, title, WorkImportSkipReason.SlugTooLong));
                continue;
            }

            var addition = ReadAddition(reader, title, slug, columns, settings, snapshot);
            errors.AddRange(reader.Errors);

            if (addition is not null && reader.Errors.Count == 0)
            {
                additions.Add(new WorkImportAddition(row.RowNumber, addition));
            }
        }

        var newCollectionRows = CollectNewCollectionRows(additions);
        var newCollections = newCollectionRows
            .OrderBy(entry => entry.Key, ImportNames.Comparer)
            .Select(entry => new WorkImportNewCollection(entry.Key, entry.Value.Count))
            .ToList();

        var checks = CollectionNameSimilarity.Check(
            [.. newCollections.Select(collection => collection.Name)],
            [.. snapshot.AllCollections.Select(collection => collection.Name.Value)]
        );

        foreach (var collision in checks.SlugCollisions)
        {
            errors.Add(
                new ImportError(
                    // the row is where the name first appears, so there is somewhere to go and fix it
                    newCollectionRows[collision.Name][0],
                    WorkImportHeaders.Collections,
                    $"\"{collision.Name}\" and \"{collision.MatchedName}\" would have the same web address. Rename one of them."
                )
            );
        }

        return new WorkImportPlan(additions, skippedRows, errors, newCollections, checks.NearDuplicates);
    }

    // which rows would join each collection the file creates. Only rows that will be imported count:
    // a skipped or broken row creates nothing
    private static Dictionary<string, List<int>> CollectNewCollectionRows(
        IReadOnlyList<WorkImportAddition> additions
    )
    {
        var rowNumbers = new Dictionary<string, List<int>>(ImportNames.Comparer);

        foreach (var addition in additions)
        {
            foreach (var name in addition.Addition.NewCollectionNames)
            {
                // the first spelling of a name is the one the collection gets
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

    private static WorkCatalogAddition? ReadAddition(
        WorkImportRowReader reader,
        string title,
        string slug,
        WorkImportColumns columns,
        WorkImportSettings settings,
        WorkImportCatalogSnapshot snapshot
    )
    {
        if (title.Length > ArtistShopLimits.WorkNameMaximumLength)
        {
            reader.AddError(WorkImportHeaders.Title, $"Titles can be at most {ArtistShopLimits.WorkNameMaximumLength} characters.");
        }
        else if (title.Length > 0 && WorkSlug.FromName(title).Value.Length == 0)
        {
            reader.AddError(WorkImportHeaders.Title, "This title has no letters or numbers to build a web address from.");
        }

        if (slug.Length > 0 && !ArtistShopSlug.IsWellFormed(slug))
        {
            reader.AddError(WorkImportHeaders.Slug, "A web address name is lower case letters, numbers and single dashes between them.");
        }

        var dateCreated = reader.Date(columns.DateCreated, WorkImportHeaders.DateCreated);
        var dimensions = ReadDimensions(reader, columns, settings.LengthUnit);
        var duration = reader.Duration(columns.Duration, WorkImportHeaders.Duration);
        var termIds = ReadTermIds(reader, columns, settings.ListSeparator);
        var collections = ReadCollections(reader, columns, settings.ListSeparator, snapshot.AllCollections);
        var product = settings.IsOneOfAKind
            ? ReadOneOfAKindProduct(reader, columns, settings.ProductTypeId)
            : ReadEditionProduct(reader, columns, settings.ProductTypeId);

        if (reader.Errors.Count > 0)
        {
            return null;
        }

        return new WorkCatalogAddition(
            snapshot.WorkType.Id,
            new WorkName(title),
            CandidateSlug(title, slug),
            reader.Text(columns.Description),
            dateCreated,
            dimensions,
            duration,
            Images: [],
            MainImageIndex: 0,
            VocabularyTermIds: termIds,
            CollectionIds: collections.ExistingIds,
            NewCollectionNames: collections.NewNames,
            Products: product is null ? [] : [product]
        );
    }

    // The row's own slug, so the work keeps its web address and a link to it from a post still
    // works; one taken here gets the next free number, as on the Add form
    private static WorkSlug CandidateSlug(string title, string slug) =>
        slug.Length > 0 ? new WorkSlug(slug) : WorkSlug.FromName(title);

    private static DimensionsCentimeters? ReadDimensions(
        WorkImportRowReader reader,
        WorkImportColumns columns,
        LengthUnit unit
    )
    {
        var height = reader.LengthInCentimeters(columns.Height, WorkImportHeaders.Height, unit);
        var width = reader.LengthInCentimeters(columns.Width, WorkImportHeaders.Width, unit);
        var depth = reader.LengthInCentimeters(columns.Depth, WorkImportHeaders.Depth, unit);

        // a cell that failed to read already has its own error
        var heightMissing = reader.Text(columns.Height) is null;
        var widthMissing = reader.Text(columns.Width) is null;

        if (heightMissing != widthMissing)
        {
            reader.AddError(heightMissing ? WorkImportHeaders.Height : WorkImportHeaders.Width, "Give both height and width, or neither.");
        }
        else if (heightMissing && reader.Text(columns.Depth) is not null)
        {
            reader.AddError(WorkImportHeaders.Depth, "A depth needs a height and width.");
        }

        return height is decimal knownHeight && width is decimal knownWidth
            ? new DimensionsCentimeters(new Dimensions(knownHeight, knownWidth, depth))
            : null;
    }

    private static List<VocabularyTermId> ReadTermIds(
        WorkImportRowReader reader,
        WorkImportColumns columns,
        char separator
    )
    {
        var termIds = new List<VocabularyTermId>();

        foreach (var (index, vocabulary) in columns.Vocabularies)
        {
            var vocabularyTermIds = new List<VocabularyTermId>();

            foreach (var name in reader.List(index, separator))
            {
                var term = vocabulary.Terms.FirstOrDefault(term => ImportNames.Comparer.Equals(term.Name.Value, name));

                if (term is null)
                {
                    reader.AddError(vocabulary.Name.Value, $"\"{name}\" isn't a term in {vocabulary.Name.Value}.");
                }
                else
                {
                    vocabularyTermIds.Add(term.Id);
                }
            }

            if (vocabulary.IsMutuallyExclusive && vocabularyTermIds.Distinct().Count() > 1)
            {
                reader.AddError(vocabulary.Name.Value, $"{vocabulary.Name.Value} allows only one term per work.");
            }

            termIds.AddRange(vocabularyTermIds);
        }

        return termIds;
    }

    private record RowCollections(List<CollectionId> ExistingIds, List<CollectionName> NewNames);

    // a name that isn't a collection yet is one the import creates; the review lists them with how many
    // rows use each, so a typo stands out before it becomes a collection of one
    private static RowCollections ReadCollections(
        WorkImportRowReader reader,
        WorkImportColumns columns,
        char separator,
        IReadOnlyList<Collection> allCollections
    )
    {
        var existingIds = new List<CollectionId>();
        var newNames = new List<CollectionName>();

        foreach (var name in reader.List(columns.Collections, separator))
        {
            var collection = allCollections.FirstOrDefault(collection => ImportNames.Comparer.Equals(collection.Name.Value, name));

            if (collection is not null)
            {
                existingIds.Add(collection.Id);
            }
            else if (name.Length > ArtistShopLimits.CollectionNameMaximumLength)
            {
                reader.AddError(WorkImportHeaders.Collections, $"Collection names can be at most {ArtistShopLimits.CollectionNameMaximumLength} characters.");
            }
            else if (CollectionSlug.FromName(name).Value.Length == 0)
            {
                reader.AddError(WorkImportHeaders.Collections, $"\"{name}\" has no letters or numbers to build a web address from.");
            }
            else
            {
                newNames.Add(new CollectionName(name));
            }
        }

        return new RowCollections(existingIds, newNames);
    }

    // edition size 1; sold means none left. A sold row may have no price
    private static ProductAddition? ReadOneOfAKindProduct(
        WorkImportRowReader reader,
        WorkImportColumns columns,
        ProductTypeId productTypeId
    )
    {
        var price = reader.Price(columns.Price, WorkImportHeaders.Price);
        var isSold = reader.Boolean(columns.Sold, WorkImportHeaders.Sold);

        if (!isSold && price is null)
        {
            return null;
        }

        return new ProductAddition(productTypeId, Label: null, price, EditionSize: 1, Stock: isSold ? 0 : 1);
    }

    // a blank edition size is an open edition
    private static ProductAddition? ReadEditionProduct(
        WorkImportRowReader reader,
        WorkImportColumns columns,
        ProductTypeId productTypeId
    )
    {
        var price = reader.Price(columns.Price, WorkImportHeaders.Price);
        var editionSize = reader.WholeNumber(columns.EditionSize, WorkImportHeaders.EditionSize, minimum: 1);
        var stock = reader.WholeNumber(columns.Stock, WorkImportHeaders.Stock, minimum: 0);

        var productCellsBlank = new[] { columns.Price, columns.EditionSize, columns.Stock }.All(column => reader.Text(column) is null);

        if (productCellsBlank)
        {
            return null;
        }

        if (reader.Text(columns.Stock) is null)
        {
            reader.AddError(WorkImportHeaders.Stock, "A row with a price or edition size needs a stock.");
            return null;
        }

        if (stock is not int knownStock)
        {
            return null;
        }

        if (knownStock > editionSize)
        {
            reader.AddError(WorkImportHeaders.Stock, "Stock can't be more than the edition size.");
        }

        if (price is null && knownStock > 0 && reader.Text(columns.Price) is null)
        {
            reader.AddError(WorkImportHeaders.Price, "A product with stock needs a price.");
        }

        return new ProductAddition(productTypeId, Label: null, price, editionSize, knownStock);
    }
}
