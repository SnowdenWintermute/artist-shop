using System.IO.Compression;
using System.Text;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Exports;

// The catalog download: a small zip of CSV files, one for each import, and a README saying how to read them
public static class CatalogExportArchive
{
    public const string ArtworkTypesFileName = "artworkTypes.csv";
    public const string VocabulariesFileName = "vocabularies.csv";
    public const string ProductsFileName = "products.csv";
    public const string ReadmeFileName = "README.txt";
    public const string ArtworksFolder = "artworks";

    // the byte order mark tells Excel the file is UTF-8, and the import skips it
    private static readonly UTF8Encoding Utf8WithByteOrderMark = new(encoderShouldEmitUTF8Identifier: true);

    // Everything goes in one folder, so unzipping gives a folder rather than loose files. Artworks
    // whose type isn't in the snapshot are left out
    public static byte[] Create(CatalogSetupSnapshot snapshot, IReadOnlyList<Artwork> artworks, string folderName)
    {
        var listSeparator = ArtworkCsvExport.ChooseListSeparator(
            CatalogSetupCsvExport.ListedNames(snapshot).Concat(ArtworkCsvExport.SeriesNames(artworks))
        );
        var artworksByType = artworks.ToLookup(artwork => artwork.Type.Id);

        using var zipBytes = new MemoryStream();

        using (var zip = new ZipArchive(zipBytes, ZipArchiveMode.Create))
        {
            AddText(zip, folderName, ReadmeFileName, Readme(listSeparator));
            AddText(zip, folderName, ArtworkTypesFileName, CatalogSetupCsvExport.ArtworkTypes(snapshot, listSeparator));
            AddText(zip, folderName, VocabulariesFileName, CatalogSetupCsvExport.Vocabularies(snapshot, listSeparator));
            AddText(zip, folderName, ProductsFileName, ArtworkCsvExport.Products(artworks));

            // a type with no artworks has nothing to move
            foreach (var type in snapshot.Types.Where(type => artworksByType.Contains(type.Id)))
            {
                AddText(
                    zip,
                    folderName,
                    $"{ArtworksFolder}/{TypeFileName(type)}",
                    ArtworkCsvExport.ForType(type, snapshot.VocabulariesFor(type.Id), artworksByType[type.Id], listSeparator)
                );
            }
        }

        return zipBytes.ToArray();
    }

    public static string TypeFileName(ArtworkTypeWithFields type) =>
        ExportFileNames.IsPortable($"{type.Name.Value}.csv") ? $"{type.Name.Value}.csv" : $"artwork-type-{type.Id.Value}.csv";

    private static void AddText(ZipArchive zip, string folderName, string path, string text)
    {
        using var entry = zip.CreateEntry($"{folderName}/{path}", CompressionLevel.Optimal).Open();
        using var writer = new StreamWriter(entry, Utf8WithByteOrderMark);
        writer.Write(text);
    }

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
          ones the artwork import reads: {ArtworkImportHeaders.Title}, {ArtworkImportHeaders.Description}, the fields the type has, {ArtworkImportHeaders.Series}, and a
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
          title within a type are skipped by the import, and products can't be imported yet.
        """;
}
