using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Exports;

// the artwork types and vocabularies as the files their imports read back, so another website can
// be set up before its artworks are imported
public static class CatalogSetupCsvExport
{
    // every name that can appear inside a list cell of these files
    public static IEnumerable<string> ListedNames(CatalogSetupSnapshot snapshot) =>
        snapshot.Fields.Select(field => field.Name)
            .Concat(snapshot.Types.Select(type => type.Name.Value))
            .Concat(snapshot.Vocabularies.SelectMany(vocabulary => vocabulary.Terms.Select(term => term.Name.Value)));

    public static string ArtworkTypes(CatalogSetupSnapshot snapshot, char listSeparator)
    {
        var csv = new CsvText();
        csv.AddRow(ArtworkTypeImportHeaders.All);

        foreach (var type in snapshot.Types.OrderBy(type => type.Name.Value, ImportNames.Comparer))
        {
            // in the order the type page lists them
            var fieldNames = snapshot.Fields
                .Where(field => type.Fields.Contains(field.Field))
                .Select(field => field.Name);

            csv.AddRow([type.Name.Value, ImportLists.Join(fieldNames, listSeparator)]);
        }

        return csv.ToString();
    }

    public static string Vocabularies(CatalogSetupSnapshot snapshot, char listSeparator)
    {
        var csv = new CsvText();
        csv.AddRow(VocabularyImportHeaders.All);

        foreach (var vocabulary in snapshot.Vocabularies.OrderBy(vocabulary => vocabulary.Name.Value, ImportNames.Comparer))
        {
            var typeNames = snapshot.Types
                .Where(type => vocabulary.ArtworkTypeIds.Contains(type.Id))
                .Select(type => type.Name.Value)
                .Order(ImportNames.Comparer);
            var termNames = vocabulary.Terms.Select(term => term.Name.Value).Order(ImportNames.Comparer);

            csv.AddRow([
                vocabulary.Name.Value,
                ImportLists.Join(typeNames, listSeparator),
                ImportLists.Join(termNames, listSeparator),
                vocabulary.IsMutuallyExclusive ? VocabularyImportHeaders.Yes : VocabularyImportHeaders.No,
            ]);
        }

        return csv.ToString();
    }
}
