using System.IO.Compression;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Exports;

// The catalog download: a small zip of CSV files, one for each import, and a README saying how to read them
public static class CatalogExportArchive
{
    public const string ArtworkTypesFileName = "artworkTypes.csv";
    public const string VocabulariesFileName = "vocabularies.csv";
    public const string ProductsFileName = "products.csv";
    public const string ArtworksFolder = "artworks";

    // Everything goes in one folder, so unzipping gives a folder rather than loose files
    public static byte[] Create(CatalogSetupSnapshot snapshot, IReadOnlyList<Artwork> artworks, string folderName)
    {
        using var zipBytes = new MemoryStream();

        using (var zip = new ZipArchive(zipBytes, ZipArchiveMode.Create))
        {
            foreach (var (path, text) in Files(snapshot, artworks))
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
        IReadOnlyList<Artwork> artworks,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        foreach (var (path, text) in Files(snapshot, artworks))
        {
            await ExportZip.AddCsvAsync(zip, $"{folderName}/{path}", text, cancellationToken);
        }
    }

    // the character that separates the names in a list cell: the first one no listed name contains
    public static char ListSeparator(CatalogSetupSnapshot snapshot, IReadOnlyList<Artwork> artworks) =>
        ArtworkCsvExport.ChooseListSeparator(CatalogSetupCsvExport.ListedNames(snapshot).Concat(ArtworkCsvExport.SeriesNames(artworks)));

    // the types that get a file in the artworks folder, each with its file's name there: a type with
    // no artworks has nothing to move, and artworks whose type isn't in the snapshot are left out
    public static IEnumerable<(string FileName, ArtworkTypeWithFields Type)> ArtworkFiles(
        CatalogSetupSnapshot snapshot,
        IReadOnlyList<Artwork> artworks
    )
    {
        var typeIds = artworks.Select(artwork => artwork.Type.Id).ToHashSet();

        return snapshot.Types.Where(type => typeIds.Contains(type.Id)).Select(type => (TypeFileName(type), type));
    }

    // each file's path inside the folder and its text
    private static IEnumerable<(string Path, string Text)> Files(CatalogSetupSnapshot snapshot, IReadOnlyList<Artwork> artworks)
    {
        var listSeparator = ListSeparator(snapshot, artworks);
        var artworksByType = artworks.ToLookup(artwork => artwork.Type.Id);

        yield return (ExportZip.ReadmeFileName, Readme(listSeparator));
        yield return (ArtworkTypesFileName, CatalogSetupCsvExport.ArtworkTypes(snapshot, listSeparator));
        yield return (VocabulariesFileName, CatalogSetupCsvExport.Vocabularies(snapshot, listSeparator));
        yield return (ProductsFileName, ArtworkCsvExport.Products(artworks));

        foreach (var (fileName, type) in ArtworkFiles(snapshot, artworks))
        {
            yield return (
                $"{ArtworksFolder}/{fileName}",
                ArtworkCsvExport.ForType(type, snapshot.VocabulariesFor(type.Id), artworksByType[type.Id], listSeparator)
            );
        }
    }

    public static string TypeFileName(ArtworkTypeWithFields type) =>
        ExportFileNames.IsPortable($"{type.Name.Value}.csv") ? $"{type.Name.Value}.csv" : $"artwork-type-{type.Id.Value}.csv";

    private static string Readme(char listSeparator) =>
        $"""
        Your catalog

        Where a cell lists several names, they are separated by "{listSeparator}".

        {ArtworkTypesFileName}
          Every artwork type and the fields it has.

        {VocabulariesFileName}
          Every vocabulary, the artwork types it applies to, and its terms.

        {ArtworksFolder}/
          One CSV file per artwork type that has artworks, with a row for each artwork. The columns are the
          ones the artwork import reads: {ArtworkImportHeaders.Title}, {ArtworkImportHeaders.Slug} (the web address name, which tells apart
          two artworks with the same title), {ArtworkImportHeaders.Description}, the fields the type has, {ArtworkImportHeaders.Series}, and a
          column for each vocabulary that applies to the type. A vocabulary whose name is also one of
          those columns, like "{ArtworkImportHeaders.Series}", is written "{ArtworkImportHeaders.VocabularyPrefix}{ArtworkImportHeaders.Series}".
          - Height, width and depth are in centimetres.
          - Dates are yyyy, yyyy-MM or yyyy-MM-dd, depending on how much of the date is known.
          - Durations are h:mm:ss, or m:ss under an hour.

        {ProductsFileName}
          Every product of every artwork: its artwork's type, title and web address name (slug, which
          tells apart two artworks with the same title), product type, label, price, edition size
          (blank means an open edition) and stock.

        A cell that starts with =, +, - or @ has a ' in front of it, so a spreadsheet shows it as text
        rather than running it as a formula. The imports here take the ' off again.

        Not included
          The order of series and of the artworks in them, series covers, and the order of each
          artwork's images or which one is shown first. The images are separate downloads on the
          Export page.

        Importing into another website here
          Import in this order, with the list separator set to "{listSeparator}" each time:
          1. {ArtworkTypesFileName}
          2. {VocabulariesFileName}
          3. Each file in {ArtworksFolder}/, choosing its type, with the unit set to centimetres and
             "one of a kind" ticked.
          Every import only adds: something that already exists is skipped. Artworks that share a
          title within a type are told apart by their {ArtworkImportHeaders.Slug}. Products can't be imported yet.
        """;
}
