using System.Globalization;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Exports;

// The works as CSV files the work import reads back, one per type, and the products beside
// them. Lengths are in centimetres, the unit they're stored in, so nothing is rounded
public static class WorkCsvExport
{
    // every one is a character the import's list separator field accepts
    private static readonly char[] ListSeparatorCandidates = [';', '|', '/', '~', '^', '#', '+'];

    public static class ProductHeaders
    {
        public const string WorkType = "workType";
        public const string Title = "title";
        public const string Slug = "slug";
        public const string ProductType = "productType";
        public const string Label = "label";
        public const string Price = "price";
        public const string EditionSize = "editionSize";
        public const string Stock = "stock";
    }

    // the first candidate no name in a list cell contains, so no cell splits a name in two
    public static char ChooseListSeparator(IEnumerable<string> listedNames)
    {
        var names = listedNames.ToList();

        foreach (var candidate in ListSeparatorCandidates)
        {
            if (!names.Any(name => name.Contains(candidate)))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Every list separator appears in a listed name.");
    }

    public static IEnumerable<string> CollectionNames(IEnumerable<Work> works) =>
        works.SelectMany(work => work.Collections.Select(collection => collection.Name.Value));

    // vocabularies are the ones that apply to the type, which become its term columns
    public static string ForType(
        WorkTypeWithFields type,
        IEnumerable<VocabularySetup> vocabularies,
        IEnumerable<Work> works,
        char listSeparator
    )
    {
        var fields = type.Fields;

        // the import refuses a column for a field the type doesn't have
        List<(string Header, Func<Work, string?> Value)> columns =
        [
            (WorkImportHeaders.Title, work => work.Name.Value),
            // tells apart two works of the type with one title, and keeps each one's web address
            (WorkImportHeaders.Slug, work => work.Slug.Value),
            (WorkImportHeaders.Description, work => work.Description),
        ];

        if (fields.Contains(WorkField.DateCreated))
        {
            columns.Add((WorkImportHeaders.DateCreated, work => work.DateCreated?.Text));
        }

        if (fields.Contains(WorkField.HeightAndWidth))
        {
            columns.Add((WorkImportHeaders.Height, work => Number(work.Dimensions?.Height)));
            columns.Add((WorkImportHeaders.Width, work => Number(work.Dimensions?.Width)));
        }

        if (fields.Contains(WorkField.Depth))
        {
            columns.Add((WorkImportHeaders.Depth, work => Number(work.Dimensions?.Depth)));
        }

        if (fields.Contains(WorkField.Duration))
        {
            columns.Add((
                WorkImportHeaders.Duration,
                work => work.Duration is TimeSpan duration ? WorkDuration.ToText(duration) : null
            ));
        }

        columns.Add((WorkImportHeaders.Collections, work => ImportLists.Join(work.Collections.Select(collection => collection.Name.Value), listSeparator)));

        foreach (var vocabulary in vocabularies.OrderBy(vocabulary => vocabulary.Name.Value, ImportNames.Comparer))
        {
            columns.Add((
                WorkImportHeaders.ForVocabulary(vocabulary.Name.Value),
                work => ImportLists.Join(
                    work.VocabularyTerms
                        .Where(term => term.VocabularyId == vocabulary.Id)
                        .Select(term => term.Name.Value),
                    listSeparator
                )
            ));
        }

        var csv = new CsvText();
        csv.AddRow(columns.Select(column => column.Header));

        foreach (var work in SortedByTitle(works))
        {
            csv.AddRow(columns.Select(column => column.Value(work)));
        }

        return csv.ToString();
    }

    // every work's products, with the slug to tell apart two works that share a title
    public static string Products(IEnumerable<Work> works)
    {
        var csv = new CsvText();
        csv.AddRow(
            [
                ProductHeaders.WorkType,
                ProductHeaders.Title,
                ProductHeaders.Slug,
                ProductHeaders.ProductType,
                ProductHeaders.Label,
                ProductHeaders.Price,
                ProductHeaders.EditionSize,
                ProductHeaders.Stock,
            ]
        );

        var sorted = works
            .OrderBy(work => work.Type.Name.Value, ImportNames.Comparer)
            .ThenBy(work => work.Name.Value, ImportNames.Comparer)
            .ThenBy(work => work.Slug.Value, StringComparer.Ordinal);

        foreach (var work in sorted)
        {
            foreach (var product in work.Products)
            {
                csv.AddRow(
                    [
                        work.Type.Name.Value,
                        work.Name.Value,
                        work.Slug.Value,
                        product.Type.Name.Value,
                        product.Label,
                        product.Price?.ToString("0.00", CultureInfo.InvariantCulture),
                        product.EditionSize?.ToString(CultureInfo.InvariantCulture),
                        product.Stock.ToString(CultureInfo.InvariantCulture),
                    ]
                );
            }
        }

        return csv.ToString();
    }

    private static IEnumerable<Work> SortedByTitle(IEnumerable<Work> works) =>
        works
            .OrderBy(work => work.Name.Value, ImportNames.Comparer)
            .ThenBy(work => work.Slug.Value, StringComparer.Ordinal);

    // the column holds 4 decimal places, which an inch value imported earlier uses
    private static string? Number(decimal? value) =>
        value?.ToString("0.####", CultureInfo.InvariantCulture);
}
