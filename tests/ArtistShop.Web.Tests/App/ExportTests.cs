using System.IO.Compression;
using System.Net;
using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Exports;
using ArtistShop.Web.Imports;
using ArtistShop.Web.Sites;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// The Export page's catalog download, and what importing it into another website gives back
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
        Assert.Contains(CatalogExportArchive.ReadmeFileName, files.Keys);
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
        IReadOnlyList<ProductAddition> products
    ) =>
        new(
            typeId,
            new ArtworkName(name),
            ArtworkSlug.FromName(name),
            description,
            DateCreated: null,
            dimensions,
            duration,
            Images: [],
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
    private static async Task<Dictionary<string, string>> ReadZipAsync(HttpResponseMessage response)
    {
        var folder = $"{Path.GetFileNameWithoutExtension(response.Content.Headers.ContentDisposition?.FileNameStar)}/";
        await using var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var files = new Dictionary<string, string>();

        foreach (var entry in zip.Entries)
        {
            Assert.StartsWith(folder, entry.FullName);
            using var reader = new StreamReader(entry.Open());
            files[entry.FullName[folder.Length..]] = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        }

        return files;
    }
}
