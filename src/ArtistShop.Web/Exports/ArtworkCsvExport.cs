using System.Globalization;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Exports;

// The artworks as CSV files the artwork import reads back, one per type, and the products beside
// them. Lengths are in centimetres, the unit they're stored in, so nothing is rounded
public static class ArtworkCsvExport
{
    // every one is a character the import's list separator field accepts
    private static readonly char[] ListSeparatorCandidates = [';', '|', '/', '~', '^', '#', '+'];

    public static class ProductHeaders
    {
        public const string ArtworkType = "artworkType";
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

    public static IEnumerable<string> SeriesNames(IEnumerable<Artwork> artworks) =>
        artworks.SelectMany(artwork => artwork.Series.Select(series => series.Name.Value));

    // vocabularies are the ones that apply to the type, which become its term columns
    public static string ForType(
        ArtworkTypeWithFields type,
        IEnumerable<VocabularySetup> vocabularies,
        IEnumerable<Artwork> artworks,
        char listSeparator
    )
    {
        var fields = type.Fields;

        string? List(IEnumerable<string> names) =>
            names.Any() ? string.Join($"{listSeparator} ", names) : null;

        // the import refuses a column for a field the type doesn't have
        List<(string Header, Func<Artwork, string?> Value)> columns =
        [
            (ArtworkImportHeaders.Title, artwork => artwork.Name.Value),
            (ArtworkImportHeaders.Description, artwork => artwork.Description),
        ];

        if (fields.Contains(ArtworkField.DateCreated))
        {
            columns.Add((ArtworkImportHeaders.DateCreated, artwork => artwork.DateCreated?.Text));
        }

        if (fields.Contains(ArtworkField.HeightAndWidth))
        {
            columns.Add((ArtworkImportHeaders.Height, artwork => Number(artwork.Dimensions?.Height)));
            columns.Add((ArtworkImportHeaders.Width, artwork => Number(artwork.Dimensions?.Width)));
        }

        if (fields.Contains(ArtworkField.Depth))
        {
            columns.Add((ArtworkImportHeaders.Depth, artwork => Number(artwork.Dimensions?.Depth)));
        }

        if (fields.Contains(ArtworkField.Duration))
        {
            columns.Add((
                ArtworkImportHeaders.Duration,
                artwork => artwork.Duration is TimeSpan duration ? ArtworkDuration.ToText(duration) : null
            ));
        }

        columns.Add((ArtworkImportHeaders.Series, artwork => List(artwork.Series.Select(series => series.Name.Value))));

        foreach (var vocabulary in vocabularies.OrderBy(vocabulary => vocabulary.Name.Value, ImportNames.Comparer))
        {
            columns.Add((
                vocabulary.Name.Value,
                artwork => List(
                    artwork.VocabularyTerms
                        .Where(term => term.VocabularyId == vocabulary.Id)
                        .Select(term => term.Name.Value)
                )
            ));
        }

        var csv = new CsvText();
        csv.AddRow(columns.Select(column => column.Header));

        foreach (var artwork in SortedByTitle(artworks))
        {
            csv.AddRow(columns.Select(column => column.Value(artwork)));
        }

        return csv.ToString();
    }

    // every artwork's products, with the slug to tell apart two artworks that share a title
    public static string Products(IEnumerable<Artwork> artworks)
    {
        var csv = new CsvText();
        csv.AddRow(
            [
                ProductHeaders.ArtworkType,
                ProductHeaders.Title,
                ProductHeaders.Slug,
                ProductHeaders.ProductType,
                ProductHeaders.Label,
                ProductHeaders.Price,
                ProductHeaders.EditionSize,
                ProductHeaders.Stock,
            ]
        );

        var sorted = artworks
            .OrderBy(artwork => artwork.Type.Name.Value, ImportNames.Comparer)
            .ThenBy(artwork => artwork.Name.Value, ImportNames.Comparer)
            .ThenBy(artwork => artwork.Slug.Value, StringComparer.Ordinal);

        foreach (var artwork in sorted)
        {
            foreach (var product in artwork.Products)
            {
                csv.AddRow(
                    [
                        artwork.Type.Name.Value,
                        artwork.Name.Value,
                        artwork.Slug.Value,
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

    private static IEnumerable<Artwork> SortedByTitle(IEnumerable<Artwork> artworks) =>
        artworks
            .OrderBy(artwork => artwork.Name.Value, ImportNames.Comparer)
            .ThenBy(artwork => artwork.Slug.Value, StringComparer.Ordinal);

    // the column holds 4 decimal places, which an inch value imported earlier uses
    private static string? Number(decimal? value) =>
        value?.ToString("0.####", CultureInfo.InvariantCulture);
}
