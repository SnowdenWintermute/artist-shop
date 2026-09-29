using System.IO.Compression;
using System.Net;
using System.Text;
using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Exports;
using ArtistShop.Web.Images;
using ArtistShop.Web.Imports;
using ArtistShop.Web.Sites;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// The Export page's downloads, and what importing the catalog into another website gives back
[Collection(TestAppCollection.Name)]
public sealed class ExportTests(TestApp app)
{
    [Fact]
    public async Task AnAdminDownloadsTheCatalogZip()
    {
        var site = await app.MakeSiteAsync();
        var catalog = Catalog(site.Id);
        await catalog.Artworks.AddAsync(
            Addition(await TypeIdAsync(catalog, "Painting"), "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [])
        );
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        var response = await client.GetAsync(ExportEndpoints.CatalogPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith($"{site.Host}-catalog-", response.Content.Headers.ContentDisposition?.FileNameStar);

        var files = await ReadZipAsync(response);
        Assert.Contains(ExportZip.ReadmeFileName, files.Keys);
        Assert.Contains(CatalogExportArchive.ArtworkTypesFileName, files.Keys);
        Assert.Contains(CatalogExportArchive.VocabulariesFileName, files.Keys);
        Assert.Contains(CatalogExportArchive.ProductsFileName, files.Keys);
        // only the types that have artworks
        Assert.Equal(
            [$"{CatalogExportArchive.ArtworksFolder}/Painting.csv"],
            files.Keys.Where(path => path.StartsWith($"{CatalogExportArchive.ArtworksFolder}/"))
        );
    }

    [Fact]
    public async Task AnAdminDownloadsATypesImagesNamedAfterTheirArtworks()
    {
        var site = await app.MakeSiteAsync();
        var catalog = Catalog(site.Id);
        var painting = await TypeIdAsync(catalog, "Painting");
        var gardens = await catalog.Series.AddAsync(new SeriesName("Gardens"), new SeriesSlug("gardens"));
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
        byte[] tiff = [(byte)'I', (byte)'I', 0x2A, 0, 4, 5, 6];
        await catalog.Artworks.AddManyAsync(
            [
                Addition(
                    painting, "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [],
                    images: [await SaveOriginalAsync(site.Id, jpeg), await SaveOriginalAsync(site.Id, tiff)]
                ),
                // in its own download
                Addition(
                    painting, "Rose", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [gardens], products: [],
                    images: [await SaveOriginalAsync(site.Id, jpeg)]
                ),
            ]
        );
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        var response = await client.GetAsync($"{ExportEndpoints.ImagesPath}?type={painting.Value}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith($"{site.Host}-images-Painting-", response.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal(["no"], response.Headers.GetValues("X-Accel-Buffering"));

        var files = await ReadZipAsync(response, ReadBytesAsync);
        Assert.Equal(["Painting/Dawn (2).tiff", "Painting/Dawn.jpg"], files.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(jpeg, files["Painting/Dawn.jpg"]);
        Assert.Equal(tiff, files["Painting/Dawn (2).tiff"]);
    }

    [Fact]
    public async Task DownloadAllHoldsEveryTypeAndSeriesInOneFolder()
    {
        var site = await app.MakeSiteAsync();
        var catalog = Catalog(site.Id);
        var painting = await TypeIdAsync(catalog, "Painting");
        var sculpture = await TypeIdAsync(catalog, "Sculpture");
        var gardens = await catalog.Series.AddAsync(new SeriesName("Gardens"), new SeriesSlug("gardens"));
        byte[] png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
        await catalog.Artworks.AddManyAsync(
            [
                Addition(
                    painting, "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [],
                    images: [await SaveOriginalAsync(site.Id, png)]
                ),
                Addition(
                    painting, "Rose", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [gardens], products: [],
                    images: [await SaveOriginalAsync(site.Id, png)]
                ),
                Addition(
                    sculpture, "Stone", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [],
                    images: [await SaveOriginalAsync(site.Id, png)]
                ),
            ]
        );
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await client.GetAsync(ExportEndpoints.AllImagesPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches($@"^{System.Text.RegularExpressions.Regex.Escape(site.Host)}-images-\d{{4}}-\d{{2}}-\d{{2}}\.zip$", response.Content.Headers.ContentDisposition?.FileNameStar);
        var files = await ReadZipAsync(response, ReadBytesAsync);
        Assert.Equal(["Painting/Dawn.png", "Painting/Gardens/Rose.png", "Sculpture/Stone.png"], files.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ASecondImageDownloadWaitsForTheFirst()
    {
        var site = await app.MakeSiteAsync();
        var catalog = Catalog(site.Id);
        var painting = await TypeIdAsync(catalog, "Painting");
        await catalog.Artworks.AddAsync(
            Addition(
                painting, "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [],
                images: [await SaveOriginalAsync(site.Id, [0xFF, 0xD8, 0xFF])]
            )
        );
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));
        var url = $"{ExportEndpoints.ImagesPath}?type={painting.Value}";

        HttpResponseMessage response;
        using (app.Services.GetRequiredService<ExportLock>().TryAcquire(site.Id))
        {
            response = await client.GetAsync(url, TestContext.Current.CancellationToken);
        }

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ExportEndpoints.ExportRunningMessage, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        // and once it has finished
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task ThePostDownloadHasAPageForEachPostWithItsImagesBesideIt()
    {
        var site = await app.MakeSiteAsync();
        var catalog = Catalog(site.Id);
        var dawnImage = await SaveOriginalAsync(site.Id, [0xFF, 0xD8, 0xFF]);
        await SaveWebCopyAsync(site.Id, dawnImage.StorageKey);
        var dawn = await catalog.Artworks.AddAsync(
            Addition(
                await TypeIdAsync(catalog, "Painting"), "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [],
                images: [dawnImage]
            )
        );
        byte[] tiff = [(byte)'I', (byte)'I', 0x2A, 0, 4, 5, 6];
        var upload = await SaveOriginalAsync(site.Id, tiff);
        var webCopy = await SaveWebCopyAsync(site.Id, upload.StorageKey);
        var posts = Posts(site.Id);
        var body = new PostBody(
            """
            {"ops":[
              {"insert":"See "},{"insert":"the gardens","attributes":{"bold":true,"link":"/series/gardens"}},{"insert":"\n"},
              {"insert":{"artshop-artwork":{"artworkId":ARTWORK_ID,"storageKey":"ARTWORK_KEY","layout":"floatLeft"}}},
              {"insert":{"artshop-image":{"storageKey":"UPLOAD_KEY","width":10,"height":10,"alt":"A study","caption":"Early"}}},
              {"insert":{"artshop-video":{"provider":"youtube","videoId":"dQw4w9WgXcQ"}}},
              {"insert":"\n"}
            ]}
            """
                .Replace("ARTWORK_ID", $"{dawn.Id.Value}")
                .Replace("ARTWORK_KEY", dawnImage.StorageKey)
                .Replace("UPLOAD_KEY", upload.StorageKey)
        );
        await posts.AddAsync(new PostTitle("Spring notes"), new PostSlug("spring-notes"), body, PostStatus.Published);
        await posts.AddAsync(new PostTitle("Unfinished"), new PostSlug("unfinished"), PostBody.Empty, PostStatus.Draft);
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        var response = await client.GetAsync(ExportEndpoints.PostsPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith($"{site.Host}-posts-", response.Content.Headers.ContentDisposition?.FileNameStar);
        var files = await ReadZipAsync(response, ReadBytesAsync);
        Assert.Equal(
            [
                "README.txt",
                "index.html",
                "spring-notes/image-1.webp",
                "spring-notes/image-2-original.tiff",
                "spring-notes/image-2.webp",
                "spring-notes/spring-notes.html",
                "unfinished/unfinished.html",
            ],
            files.Keys.Order(StringComparer.Ordinal)
        );
        Assert.Equal(tiff, files["spring-notes/image-2-original.tiff"]);
        Assert.Equal(webCopy, files["spring-notes/image-2.webp"]);

        var page = Encoding.UTF8.GetString(files["spring-notes/spring-notes.html"]);
        Assert.Contains($"""<a href="http://{site.Host}/series/gardens"><strong>the gardens</strong></a>""", page);
        Assert.Contains("""<figure class="floatLeft" style="width: 400px"><img src="image-1.webp" alt="Dawn" width="400" height="400"></figure>""", page);
        Assert.Contains("""<a href="image-2-original.tiff"><img src="image-2.webp" alt="A study" """, page);
        Assert.Contains("<figcaption>Early</figcaption>", page);
        Assert.Contains("""<a href="https://www.youtube.com/watch?v=dQw4w9WgXcQ">""", page);

        var index = Encoding.UTF8.GetString(files["index.html"]);
        Assert.Contains("""<a href="spring-notes/spring-notes.html">Spring notes</a>""", index);
        Assert.Contains("""<h2>Drafts</h2>""", index);
        Assert.Contains("""<a href="unfinished/unfinished.html">Unfinished</a>""", index);
    }

    [Fact]
    public async Task AnImageShownTwiceIsOneFileAndOneWithoutAWebCopyIsLeftOut()
    {
        var site = await app.MakeSiteAsync();
        var shown = await SaveOriginalAsync(site.Id, [0xFF, 0xD8, 0xFF]);
        await SaveWebCopyAsync(site.Id, shown.StorageKey);
        var withoutWebCopy = await SaveOriginalAsync(site.Id, [0xFF, 0xD8, 0xFF]);
        var body = new PostBody(
            """
            {"ops":[
              {"insert":{"artshop-image":{"storageKey":"SHOWN_KEY","width":10,"height":10,"alt":"First"}}},
              {"insert":{"artshop-image":{"storageKey":"MISSING_KEY","width":10,"height":10,"alt":"Gone"}}},
              {"insert":{"artshop-image":{"storageKey":"SHOWN_KEY","width":10,"height":10,"alt":"Again"}}},
              {"insert":"\n"}
            ]}
            """
                .Replace("SHOWN_KEY", shown.StorageKey)
                .Replace("MISSING_KEY", withoutWebCopy.StorageKey)
        );
        await Posts(site.Id).AddAsync(new PostTitle("Twice"), new PostSlug("twice"), body, PostStatus.Published);
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        var files = await ReadZipAsync(await client.GetAsync(ExportEndpoints.PostsPath, TestContext.Current.CancellationToken));

        Assert.Equal(
            ["README.txt", "index.html", "twice/image-1-original.jpg", "twice/image-1.webp", "twice/twice.html"],
            files.Keys.Order(StringComparer.Ordinal)
        );
        var page = files["twice/twice.html"];
        Assert.Contains("""<img src="image-1.webp" alt="First" """, page);
        Assert.Contains("""<img src="image-1.webp" alt="Again" """, page);
        Assert.DoesNotContain("Gone", page);
    }

    [Fact]
    public async Task SomeoneWhoIsntAnAdminIsDenied()
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAccountAsync());

        var response = await client.GetAsync(ExportEndpoints.CatalogPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task SomeoneNotSignedInIsSentToSignIn()
    {
        var site = await app.MakeSiteAsync();

        var response = await app.ClientFor(site.Host).GetAsync(ExportEndpoints.CatalogPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    // Exported from one website and imported into a new one in the README's order (types, then
    // vocabularies, then each type's artworks), the catalog comes back the same
    [Fact]
    public async Task ImportingTheExportIntoAnotherWebsiteGivesTheSameCatalog()
    {
        var from = await app.MakeSiteAsync();
        var fromCatalog = Catalog(from.Id);
        var painting = await TypeIdAsync(fromCatalog, "Painting");
        var installation = await fromCatalog.Types.AddAsync(
            new ArtworkTypeName("Installation"),
            [ArtworkField.DateCreated, ArtworkField.HeightAndWidth, ArtworkField.Depth, ArtworkField.Duration]
        );
        var medium = await fromCatalog.Vocabularies.AddAsync(new VocabularyName("Medium"), [painting, installation]);
        var oil = await fromCatalog.Terms.AddAsync(medium, new VocabularyTermName("Oil"));
        var bronze = await fromCatalog.Terms.AddAsync(medium, new VocabularyTermName("Bronze"));
        await fromCatalog.Terms.AddAsync(medium, new VocabularyTermName("Unused"));
        var gardens = await fromCatalog.Series.AddAsync(new SeriesName("Gardens; Summer"), new SeriesSlug("gardens-summer"));
        var original = (await fromCatalog.ProductTypes.GetAllAsync()).Single(type => type.IsDefault);

        await fromCatalog.Artworks.AddManyAsync(
            [
                Addition(
                    painting,
                    "Roses, at dusk",
                    "Painted \"en plein air\".\nVarnished in 2020.",
                    new DimensionsCentimeters(new Dimensions(60.96m, 45.72m, null)),
                    duration: null,
                    termIds: [oil],
                    seriesIds: [gardens],
                    products: [new ProductAddition(original.Id, Label: null, 950m, EditionSize: 1, Stock: 1)]
                ),
                Addition(painting, "=Untitled", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: []),
                Addition(
                    installation,
                    "Room of echoes",
                    description: null,
                    new DimensionsCentimeters(new Dimensions(300m, 400m, 250.5m)),
                    TimeSpan.FromMinutes(12),
                    termIds: [bronze],
                    seriesIds: [gardens],
                    products: []
                ),
            ]
        );

        var client = await app.SignedInClientAsync(from.Host, from.OwnerEmail);
        var files = await ReadZipAsync(await client.GetAsync(ExportEndpoints.CatalogPath, TestContext.Current.CancellationToken));
        Assert.Contains("Painting,\"Roses, at dusk\",roses-at-dusk,Original,,950.00,1,1", files[CatalogExportArchive.ProductsFileName]);
        // a spreadsheet would run it as a formula without the '
        Assert.Contains("\"'=Untitled\"", files[$"{CatalogExportArchive.ArtworksFolder}/Painting.csv"]);

        var to = await app.MakeSiteAsync();
        var toCatalog = Catalog(to.Id);
        // the series name has a semicolon, so the export chose the next separator
        const char listSeparator = '|';

        var typePlan = ArtworkTypeImportPlanner.Plan(files[CatalogExportArchive.ArtworkTypesFileName], listSeparator, await SetupAsync(toCatalog));
        Assert.Empty(typePlan.Errors);
        await ArtworkTypeImportPlanner.ApplyAsync(typePlan, toCatalog.Types);

        var vocabularyPlan = VocabularyImportPlanner.Plan(files[CatalogExportArchive.VocabulariesFileName], listSeparator, await SetupAsync(toCatalog));
        Assert.Empty(vocabularyPlan.Errors);
        await VocabularyImportPlanner.ApplyAsync(vocabularyPlan, toCatalog.Vocabularies, toCatalog.Terms);

        foreach (var typeName in (string[])["Painting", "Installation"])
        {
            var snapshot = await ArtworkImportCatalogSnapshot.LoadAsync(
                await TypeIdAsync(toCatalog, typeName),
                toCatalog.Types,
                toCatalog.Vocabularies,
                toCatalog.Series,
                toCatalog.ProductTypes,
                toCatalog.Artworks
            );
            Assert.NotNull(snapshot);
            var plan = ArtworkImportPlanner.Plan(
                files[$"{CatalogExportArchive.ArtworksFolder}/{typeName}.csv"],
                new ArtworkImportSettings(LengthUnit.Centimeters, original.Id, IsOneOfAKind: true, listSeparator),
                snapshot
            );
            Assert.Empty(plan.Errors);
            await toCatalog.Artworks.AddManyAsync([.. plan.Additions.Select(addition => addition.Addition)]);
        }

        var fromSetup = await SetupAsync(fromCatalog);
        var toSetup = await SetupAsync(toCatalog);
        Assert.Equal(CatalogSetupCsvExport.ArtworkTypes(fromSetup, listSeparator), CatalogSetupCsvExport.ArtworkTypes(toSetup, listSeparator));
        Assert.Equal(CatalogSetupCsvExport.Vocabularies(fromSetup, listSeparator), CatalogSetupCsvExport.Vocabularies(toSetup, listSeparator));
        Assert.Equal(Describe(await fromCatalog.Artworks.GetAllAsync()), Describe(await toCatalog.Artworks.GetAllAsync()));
    }

    private static ArtworkCatalogAddition Addition(
        ArtworkTypeId typeId,
        string name,
        string? description,
        DimensionsCentimeters? dimensions,
        TimeSpan? duration,
        IReadOnlyList<VocabularyTermId> termIds,
        IReadOnlyList<SeriesId> seriesIds,
        IReadOnlyList<ProductAddition> products,
        IReadOnlyList<ArtworkImage>? images = null
    ) =>
        new(
            typeId,
            new ArtworkName(name),
            ArtworkSlug.FromName(name),
            description,
            DateCreated: null,
            dimensions,
            duration,
            Images: images ?? [],
            MainImageIndex: 0,
            termIds,
            seriesIds,
            NewSeriesNames: [],
            products
        );

    private sealed record SiteCatalog(
        ArtworkRepository Artworks,
        ArtworkFieldRepository Fields,
        ArtworkTypeRepository Types,
        VocabularyRepository Vocabularies,
        VocabularyTermRepository Terms,
        SeriesRepository Series,
        ProductTypeRepository ProductTypes
    );

    private SiteCatalog Catalog(SiteId siteId)
    {
        SiteDatabase database = app.Services.GetRequiredService<SiteDatabases>().For(siteId);
        return new SiteCatalog(
            new ArtworkRepository(database),
            new ArtworkFieldRepository(database),
            new ArtworkTypeRepository(database),
            new VocabularyRepository(database),
            new VocabularyTermRepository(database),
            new SeriesRepository(database),
            new ProductTypeRepository(database)
        );
    }

    // an original on the site's disk, as an upload leaves it
    private async Task<ArtworkImage> SaveOriginalAsync(SiteId siteId, byte[] bytes)
    {
        var storageKey = Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(
            ImageStorage.ForSite(app.Services.GetRequiredService<ImageStorageSettings>(), siteId).OriginalPath(storageKey),
            bytes,
            TestContext.Current.CancellationToken
        );

        return new ArtworkImage(storageKey, OriginalFileName: null, Width: 10, Height: 10, BlurDataUri: null);
    }

    private PostRepository Posts(SiteId siteId) =>
        new(app.Services.GetRequiredService<SiteDatabases>().For(siteId));

    // the web copy an upload of an image 10 pixels wide gets, at its own width
    private async Task<byte[]> SaveWebCopyAsync(SiteId siteId, string storageKey)
    {
        var directory = ImageStorage.ForSite(app.Services.GetRequiredService<ImageStorageSettings>(), siteId).VariantDirectory(storageKey);
        Directory.CreateDirectory(directory);
        byte[] bytes = [(byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P', 1];
        await File.WriteAllBytesAsync(Path.Combine(directory, ImageVariants.FileName(10, ImageVariantFormat.Webp)), bytes, TestContext.Current.CancellationToken);

        return bytes;
    }

    private static Task<CatalogSetupSnapshot> SetupAsync(SiteCatalog catalog) =>
        CatalogSetupSnapshot.LoadAsync(catalog.Fields, catalog.Types, catalog.Vocabularies);

    private static async Task<ArtworkTypeId> TypeIdAsync(SiteCatalog catalog, string name) =>
        (await catalog.Types.GetAllAsync()).Single(type => type.Name.Value == name).Id;

    // what an artwork holds, less its ids, which differ between websites, and its products, which the
    // import can't bring back
    private static List<string> Describe(IEnumerable<Artwork> artworks) =>
        [
            .. artworks
                .Select(artwork => string.Join(
                    " | ",
                    artwork.Type.Name.Value,
                    artwork.Name.Value,
                    artwork.Slug.Value,
                    artwork.Description,
                    artwork.DateCreated?.Text,
                    artwork.Dimensions?.Text,
                    artwork.Duration,
                    string.Join(", ", artwork.Series.Select(series => series.Name.Value)),
                    string.Join(", ", artwork.VocabularyTerms.Select(term => $"{term.VocabularyName.Value}: {term.Name.Value}"))
                ))
                .Order(),
        ];

    // each file by its path inside the zip's one folder, which is named after the download, so
    // unzipping it doesn't scatter files wherever it's unzipped
    private static Task<Dictionary<string, string>> ReadZipAsync(HttpResponseMessage response) =>
        ReadZipAsync(response, ReadTextAsync);

    private static async Task<string> ReadTextAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<byte[]> ReadBytesAsync(Stream stream)
    {
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes, TestContext.Current.CancellationToken);
        return bytes.ToArray();
    }

    private static async Task<Dictionary<string, T>> ReadZipAsync<T>(HttpResponseMessage response, Func<Stream, Task<T>> read)
    {
        var folder = $"{Path.GetFileNameWithoutExtension(response.Content.Headers.ContentDisposition?.FileNameStar)}/";
        await using var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var files = new Dictionary<string, T>();

        foreach (var entry in zip.Entries)
        {
            Assert.StartsWith(folder, entry.FullName);
            await using var entryStream = entry.Open();
            files[entry.FullName[folder.Length..]] = await read(entryStream);
        }

        return files;
    }
}
