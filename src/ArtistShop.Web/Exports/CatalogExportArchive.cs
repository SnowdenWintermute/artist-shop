using System.IO.Compression;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Exports;

// The catalog download: a small zip of CSV files, one for each import, and a README saying how to read them
public static class CatalogExportArchive
{
    public const string WorkTypesFileName = "workTypes.csv";
    public const string VocabulariesFileName = "vocabularies.csv";
    public const string ProductsFileName = "products.csv";
    public const string WorksFolder = "works";

    // Everything goes in one folder, so unzipping gives a folder rather than loose files
    public static byte[] Create(CatalogSetupSnapshot snapshot, IReadOnlyList<Work> works, string folderName)
    {
        using var zipBytes = new MemoryStream();

        using (var zip = new ZipArchive(zipBytes, ZipArchiveMode.Create))
        {
            foreach (var (path, text) in Files(snapshot, works))
            {
                using var entry = zip.CreateEntry($"{folderName}/{path}", CompressionLevel.Optimal).Open();
                using var writer = new StreamWriter(entry, ExportZip.Utf8WithByteOrderMark);
                writer.Write(text);
            }
        }

        return zipBytes.ToArray();
    }

    // the same files, into a zip being written, as the whole-website download does
    public static async Task AddAsync(
        ZipArchive zip,
        CatalogSetupSnapshot snapshot,
        IReadOnlyList<Work> works,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        foreach (var (path, text) in Files(snapshot, works))
        {
            await ExportZip.AddCsvAsync(zip, $"{folderName}/{path}", text, cancellationToken);
        }
    }

    // the character that separates the names in a list cell: the first one no listed name contains
    public static char ListSeparator(CatalogSetupSnapshot snapshot, IReadOnlyList<Work> works) =>
        WorkCsvExport.ChooseListSeparator(CatalogSetupCsvExport.ListedNames(snapshot).Concat(WorkCsvExport.CollectionNames(works)));

    // the types that get a file in the works folder, each with its file's name there: a type with
    // no works has nothing to move, and works whose type isn't in the snapshot are left out
    public static IEnumerable<(string FileName, WorkTypeWithFields Type)> WorkFiles(
        CatalogSetupSnapshot snapshot,
        IReadOnlyList<Work> works
    )
    {
        var typeIds = works.Select(work => work.Type.Id).ToHashSet();

        return snapshot.Types.Where(type => typeIds.Contains(type.Id)).Select(type => (TypeFileName(type), type));
    }

    // each file's path inside the folder and its text
    private static IEnumerable<(string Path, string Text)> Files(CatalogSetupSnapshot snapshot, IReadOnlyList<Work> works)
    {
        var listSeparator = ListSeparator(snapshot, works);
        var worksByType = works.ToLookup(work => work.Type.Id);

        yield return (ExportZip.ReadmeFileName, Readme(listSeparator));
        yield return (WorkTypesFileName, CatalogSetupCsvExport.WorkTypes(snapshot, listSeparator));
        yield return (VocabulariesFileName, CatalogSetupCsvExport.Vocabularies(snapshot, listSeparator));
        yield return (ProductsFileName, WorkCsvExport.Products(works));

        foreach (var (fileName, type) in WorkFiles(snapshot, works))
        {
            yield return (
                $"{WorksFolder}/{fileName}",
                WorkCsvExport.ForType(type, snapshot.VocabulariesFor(type.Id), worksByType[type.Id], listSeparator)
            );
        }
    }

    public static string TypeFileName(WorkTypeWithFields type) =>
        ExportFileNames.IsPortable($"{type.Name.Value}.csv") ? $"{type.Name.Value}.csv" : $"work-type-{type.Id.Value}.csv";

    private static string Readme(char listSeparator) =>
        $"""
        Your catalog

        Where a cell lists several names, they are separated by "{listSeparator}".

        {WorkTypesFileName}
          Every work type and the fields it has.

        {VocabulariesFileName}
          Every vocabulary, the work types it applies to, and its terms.

        {WorksFolder}/
          One CSV file per work type that has works, with a row for each work. The columns are the
          ones the work import reads: {WorkImportHeaders.Title}, {WorkImportHeaders.Slug} (the web address name, which tells apart
          two works with the same title), {WorkImportHeaders.Description}, the fields the type has, {WorkImportHeaders.Collections}, and a
          column for each vocabulary that applies to the type. A vocabulary whose name is also one of
          those columns, like "{WorkImportHeaders.Collections}", is written "{WorkImportHeaders.VocabularyPrefix}{WorkImportHeaders.Collections}".
          - Height, width and depth are in centimetres.
          - Dates are yyyy, yyyy-MM or yyyy-MM-dd, depending on how much of the date is known.
          - Durations are h:mm:ss, or m:ss under an hour.

        {ProductsFileName}
          Every product of every work: its work's type, title and web address name (slug, which
          tells apart two works with the same title), product type, label, price, edition size
          (blank means an open edition) and stock.

        A cell that starts with =, +, - or @ has a ' in front of it, so a spreadsheet shows it as text
        rather than running it as a formula. The imports here take the ' off again.

        Not included
          The order of collections and of the works in them, collection covers, and the order of each
          work's images or which one is shown first. The images are separate downloads on the
          Export page.

        Importing into another website here
          Import in this order, with the list separator set to "{listSeparator}" each time:
          1. {WorkTypesFileName}
          2. {VocabulariesFileName}
          3. Each file in {WorksFolder}/, choosing its type, with the unit set to centimetres and
             "one of a kind" checked.
          Every import only adds: something that already exists is skipped. Works that share a
          title within a type are told apart by their {WorkImportHeaders.Slug}. Products can't be imported yet.
        """;
}
