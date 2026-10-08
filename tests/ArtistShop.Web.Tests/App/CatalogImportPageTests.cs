using System.Net;
using System.Text.RegularExpressions;
using ArtistShop.Web.Components;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Sites;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// the artwork type and vocabulary import pages; the planner and export tests cover what each plans
[Collection(TestAppCollection.Name)]
public sealed partial class CatalogImportPageTests(TestApp app)
{
    [Theory]
    [InlineData(PageUrls.ArtworkTypeImport, "Import artwork types from a spreadsheet")]
    [InlineData(PageUrls.VocabularyImport, "Import vocabularies from a spreadsheet")]
    public async Task AnAdminSeesThePage(string path, string heading)
    {
        var client = await app.SignedInClientAsync(TestApp.FirstSiteHost, await app.MakeFirstSiteAdminAsync());

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(heading, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // check, then confirm with the plan the review carried, as the review's Import button does. The
    // file goes in as the kept text an earlier check leaves in the form, so no upload is needed
    [Fact]
    public async Task CheckingThenConfirmingImportsTheFile()
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var (reviewPage, confirmed) = await CheckThenConfirmAsync(client, PageUrls.ArtworkTypeImport, "artworkType,fields\nVideo,Duration\n");

        Assert.Contains("Import 1 artwork type", reviewPage);
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
        Assert.Contains("imported=1", confirmed.Headers.Location?.Query);
        var types = await new ArtworkTypeRepository(app.Services.GetRequiredService<SiteDatabases>().For(site.Id)).GetAllAsync();
        Assert.Contains(types, type => type.Name.Value == "Video");
    }

    // an existing vocabulary gains the type it's missing and keeps the ones it has
    [Fact]
    public async Task AnExistingVocabularyGainsAnArtworkType()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var types = await new ArtworkTypeRepository(database).GetAllAsync();
        ArtworkTypeId TypeId(string name) => types.Single(type => type.Name.Value == name).Id;
        var vocabularies = new VocabularyRepository(database);
        var medium = await vocabularies.AddAsync(new VocabularyName("Medium"), isSingleChoice: false, [TypeId("Painting"), TypeId("Photograph")]);
        await new VocabularyTermRepository(database).AddAsync(medium, new VocabularyTermName("Oil"));
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var (reviewPage, confirmed) = await CheckThenConfirmAsync(
            client,
            PageUrls.VocabularyImport,
            "vocabulary,artworkTypes,terms\nMedium,Painting; Photograph; Sculpture,Oil\n"
        );

        Assert.Contains("Import 1 vocabulary", reviewPage);
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
        var saved = await vocabularies.GetAsync(medium);
        Assert.NotNull(saved);
        Assert.Equal(
            [TypeId("Painting"), TypeId("Photograph"), TypeId("Sculpture")],
            saved.ArtworkTypeIds.OrderBy(id => id.Value)
        );
    }

    // the review page, and the answer to confirming the plan it showed
    private static async Task<(string ReviewPage, HttpResponseMessage Confirmed)> CheckThenConfirmAsync(
        HttpClient client,
        string path,
        string csv
    )
    {
        var review = await TestApp.PostFormAsync(
            client,
            path,
            "check-catalog-import",
            new() { ["Check.KeptCsvText"] = csv, ["Check.KeptFileName"] = "import.csv", ["Check.ListSeparator"] = ";" }
        );
        var reviewPage = await review.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var fingerprint = FingerprintField().Match(reviewPage).Groups["fingerprint"].Value;

        var confirmed = await TestApp.PostFormAsync(
            client,
            path,
            "confirm-catalog-import",
            new() { ["Confirm.CsvText"] = csv, ["Confirm.Fingerprint"] = fingerprint, ["Confirm.ListSeparator"] = ";" }
        );

        return (reviewPage, confirmed);
    }

    [GeneratedRegex("""name="Confirm.Fingerprint" value="(?<fingerprint>[0-9A-F]+)""")]
    private static partial Regex FingerprintField();

    [Theory]
    [InlineData(PageUrls.ArtworkTypeImport)]
    [InlineData(PageUrls.VocabularyImport)]
    public async Task SomeoneWhoIsntAnAdminIsDenied(string path)
    {
        var client = await app.SignedInClientAsync(TestApp.FirstSiteHost, await app.MakeAccountAsync());

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", response.Headers.Location?.AbsolutePath);
    }
}
