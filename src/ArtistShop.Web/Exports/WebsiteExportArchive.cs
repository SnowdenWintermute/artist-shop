using System.Text.Json.Nodes;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Images;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Exports;

// The whole-website download: the catalog, image and post downloads side by side in one folder,
// each laid out as its own download, for the whole-website import to read in one go
public static class WebsiteExportArchive
{
    public const string CatalogFolder = "catalog";
    public const string ImagesFolder = "images";
    public const string PostsFolder = "posts";

    // What the whole-website import needs that the files don't say themselves: the list separator,
    // and each artwork CSV's type, since a type whose name can't be a file name has a file named
    // after its id on this website
    public const string ManifestFileName = "website.json";
    public const int FormatVersion = 1;
    public const string FormatVersionProperty = "formatVersion";
    public const string ListSeparatorProperty = "listSeparator";
    public const string ArtworkFilesProperty = "artworkFiles";
    public const string FileProperty = "file";
    public const string ArtworkTypeProperty = "artworkType";

    public static async Task WriteAsync(
        Stream destination,
        CatalogSetupSnapshot snapshot,
        IReadOnlyList<Artwork> artworks,
        IReadOnlyList<ExportedPost> posts,
        IReadOnlyDictionary<string, ArtworkImageWithArtwork> artworkImages,
        string folderName,
        string host,
        string siteOrigin,
        ImageStorage imageStorage,
        CancellationToken cancellationToken
    )
    {
        await using var zip = await ExportZip.CreateAsync(destination, cancellationToken);

        await ExportZip.AddTextAsync(zip, $"{folderName}/{ExportZip.ReadmeFileName}", Readme(host), cancellationToken);
        await ExportZip.AddTextAsync(zip, $"{folderName}/{ManifestFileName}", Manifest(snapshot, artworks), cancellationToken);
        await CatalogExportArchive.AddAsync(zip, snapshot, artworks, $"{folderName}/{CatalogFolder}", cancellationToken);
        await ImageExportArchive.AddAsync(
            zip,
            ImageExportPlan.Parts(artworks).SelectMany(part => part.Entries),
            $"{folderName}/{ImagesFolder}",
            imageStorage,
            cancellationToken
        );
        await PostExportArchive.AddAsync(zip, posts, artworkImages, $"{folderName}/{PostsFolder}", host, siteOrigin, imageStorage, cancellationToken);
    }

    public static string Manifest(CatalogSetupSnapshot snapshot, IReadOnlyList<Artwork> artworks)
    {
        var artworkFiles = new JsonArray();

        foreach (var (fileName, type) in CatalogExportArchive.ArtworkFiles(snapshot, artworks))
        {
            artworkFiles.Add(new JsonObject { [FileProperty] = fileName, [ArtworkTypeProperty] = type.Name.Value });
        }

        var manifest = new JsonObject
        {
            [FormatVersionProperty] = FormatVersion,
            [ListSeparatorProperty] = CatalogExportArchive.ListSeparator(snapshot, artworks).ToString(),
            [ArtworkFilesProperty] = artworkFiles,
        };

        return manifest.ToJsonString(PostExportJson.Readable);
    }

    private static string Readme(string host) =>
        $"""
        Everything from {host}

        {ManifestFileName}
          What the import needs to read the other files. Don't change it by hand.

        {CatalogFolder}/
          Artwork types, vocabularies, artworks and products as CSV files. Its README explains them.

        {ImagesFolder}/
          The original of every artwork image, in a folder for each artwork type and series.
          {ImageExportArchive.ImageListFileName} lists each file with its artwork.

        {PostsFolder}/
          Every post, drafts included, as a web page in a folder with its images. Open index.html
          in a web browser to read them.

        Importing into another website here
          Choose this whole folder, unzipped, under Import > Move a whole website.
        """;
}
