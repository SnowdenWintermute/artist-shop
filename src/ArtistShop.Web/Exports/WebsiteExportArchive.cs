using System.Text.Json.Nodes;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;
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
    // each work CSV's type, since a type whose name can't be a file name has a file named after its
    // id on this website, and the website's wording
    public const string ManifestFileName = "website.json";
    public const int FormatVersion = 1;
    public const string FormatVersionProperty = "formatVersion";
    public const string ListSeparatorProperty = "listSeparator";
    public const string WorkFilesProperty = "workFiles";
    public const string FileProperty = "file";
    public const string WorkTypeProperty = "workType";
    public const string WordingProperty = "wording";
    public const string CollectionWordingProperty = "collection";
    public const string WorkWordingProperty = "work";
    public const string SingularProperty = "singular";
    public const string PluralProperty = "plural";
    public const string KeepsCaseProperty = "keepsCase";

    public static async Task WriteAsync(
        Stream destination,
        CatalogSetupSnapshot snapshot,
        IReadOnlyList<Work> works,
        SiteWording wording,
        IReadOnlyList<ExportedPost> posts,
        IReadOnlyDictionary<string, WorkImageWithWork> workImages,
        string folderName,
        string host,
        string siteOrigin,
        ImageStorage imageStorage,
        CancellationToken cancellationToken
    )
    {
        await using var zip = await ExportZip.CreateAsync(destination, cancellationToken);

        await ExportZip.AddTextAsync(zip, $"{folderName}/{ExportZip.ReadmeFileName}", Readme(host), cancellationToken);
        await ExportZip.AddTextAsync(zip, $"{folderName}/{ManifestFileName}", Manifest(snapshot, works, wording), cancellationToken);
        await CatalogExportArchive.AddAsync(zip, snapshot, works, $"{folderName}/{CatalogFolder}", cancellationToken);
        await ImageExportArchive.AddAsync(
            zip,
            ImageExportPlan.Parts(works).SelectMany(part => part.Entries),
            $"{folderName}/{ImagesFolder}",
            imageStorage,
            cancellationToken
        );
        await PostExportArchive.AddAsync(zip, posts, workImages, $"{folderName}/{PostsFolder}", host, siteOrigin, imageStorage, cancellationToken);
    }

    public static string Manifest(CatalogSetupSnapshot snapshot, IReadOnlyList<Work> works, SiteWording wording)
    {
        var workFiles = new JsonArray();

        foreach (var (fileName, type) in CatalogExportArchive.WorkFiles(snapshot, works))
        {
            workFiles.Add(new JsonObject { [FileProperty] = fileName, [WorkTypeProperty] = type.Name.Value });
        }

        var manifest = new JsonObject
        {
            [FormatVersionProperty] = FormatVersion,
            [ListSeparatorProperty] = CatalogExportArchive.ListSeparator(snapshot, works).ToString(),
            [WorkFilesProperty] = workFiles,
            [WordingProperty] = new JsonObject
            {
                [CollectionWordingProperty] = NounObject(wording.CollectionChoice),
                [WorkWordingProperty] = NounObject(wording.WorkChoice),
            },
        };

        return manifest.ToJsonString(PostExportJson.Readable);
    }

    // null words stay null, so a default carries over as the default rather than as today's words
    private static JsonObject NounObject(NounChoice choice) =>
        new()
        {
            [SingularProperty] = choice.Singular,
            [PluralProperty] = choice.Plural,
            [KeepsCaseProperty] = choice.KeepsCase,
        };

    private static string Readme(string host) =>
        $"""
        Everything from {host}

        {ManifestFileName}
          What the import needs to read the other files. Don't change it by hand.

        {CatalogFolder}/
          Work types, vocabularies, works and products as CSV files. Its README explains them.

        {ImagesFolder}/
          The original of every work image, in a folder for each work type and collection.
          {ImageExportArchive.ImageListFileName} lists each file with its work.

        {PostsFolder}/
          Every post, drafts included, as a web page in a folder with its images. Open index.html
          in a web browser to read them.

        Importing into another website here
          Choose this whole folder, unzipped, under Import > Move a whole website.
        """;
}
