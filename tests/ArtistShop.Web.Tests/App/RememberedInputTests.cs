using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ArtistShop.Web.Components;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Components.Catalog;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// The work type every type select shares: a page without one in its address starts at the type
// its admin last chose
[Collection(TestAppCollection.Name)]
public sealed partial class RememberedInputTests(TestApp app)
{
    private const string AddPath = PageUrls.AddWork;

    private async Task<(CatalogTestData Catalog, HttpClient Client)> SiteWithAdminAsync()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        return (new CatalogTestData(database), client);
    }

    // as RememberedSelect's script sends it: with the token the page put on the element
    private static async Task<HttpResponseMessage> SaveAsync(HttpClient client, CatalogTestData catalog, string inputName, string value)
    {
        var page = await client.GetStringAsync(
            $"{AddPath}?type={(await catalog.GetPaintingTypeIdAsync()).Value}",
            TestContext.Current.CancellationToken
        );
        var token = SaveToken().Match(page);

        return await client.PostAsync(
            RememberedInputEndpoints.Path,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    [RememberedInputEndpoints.NameField] = inputName,
                    [RememberedInputEndpoints.ValueField] = value,
                    [token.Groups["field"].Value] = token.Groups["token"].Value,
                }
            ),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task AddOneGoesToTheSavedWorkType()
    {
        var (catalog, client) = await SiteWithAdminAsync();
        var photograph = (await catalog.GetTypeIdAsync("Photograph")).Value.ToString(CultureInfo.InvariantCulture);

        var response = await SaveAsync(client, catalog, RememberedInputs.WorkType, photograph);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.EndsWith($"?type={photograph}", await RedirectFromAddAsync(client));
    }

    // a type deleted since it was saved is no choice any more, so the page goes to the first by name, as for nobody
    [Fact]
    public async Task ASavedTypeSinceDeletedGoesToTheFirstByName()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        await app
            .Services.GetRequiredService<SiteMemberInputValueRepository>()
            .SetAsync(site.Id, await app.UserIdAsync(site.OwnerEmail), RememberedInputs.WorkType, "999999");
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        var first = WorkTypeOrder.SortedByName(await new WorkTypeRepository(database).GetAllAsync())[0];

        Assert.EndsWith($"?type={first.Id.Value}", await RedirectFromAddAsync(client));
    }

    // the address decides the type, so the select shows it whatever was saved
    [Fact]
    public async Task TheSelectShowsTheTypeInTheAddress()
    {
        var (catalog, client) = await SiteWithAdminAsync();
        var photograph = (await catalog.GetTypeIdAsync("Photograph")).Value.ToString(CultureInfo.InvariantCulture);
        await SaveAsync(client, catalog, RememberedInputs.WorkType, photograph);
        var painting = (await catalog.GetPaintingTypeIdAsync()).Value.ToString(CultureInfo.InvariantCulture);

        var page = await client.GetStringAsync($"{AddPath}?type={painting}", TestContext.Current.CancellationToken);

        Assert.Contains("<remembered-select", page);
        Assert.Equal([painting], SelectedOptions(page));
    }

    [Fact]
    public async Task ImportGoesToTheSavedWorkType()
    {
        var (catalog, client) = await SiteWithAdminAsync();
        var photograph = (await catalog.GetTypeIdAsync("Photograph")).Value.ToString(CultureInfo.InvariantCulture);
        await SaveAsync(client, catalog, RememberedInputs.WorkType, photograph);

        Assert.EndsWith($"?type={photograph}", await RedirectFromAsync(client, PageUrls.WorkImport));
    }

    // nothing saved, so the page asks rather than choosing for them
    [Fact]
    public async Task ImportWithNothingSavedAsksForAType()
    {
        var (_, client) = await SiteWithAdminAsync();

        var page = await client.GetStringAsync(PageUrls.WorkImport, TestContext.Current.CancellationToken);

        Assert.Contains("disabled selected>Choose a type</option>", RememberedSelectOf(page));
    }

    [Fact]
    public async Task TheTablePageStartsAtTheSavedWorkType()
    {
        var (catalog, client) = await SiteWithAdminAsync();
        var photograph = (await catalog.GetTypeIdAsync("Photograph")).Value.ToString(CultureInfo.InvariantCulture);
        await SaveAsync(client, catalog, RememberedInputs.WorkType, photograph);

        var page = await client.GetStringAsync(PageUrls.WorkTable, TestContext.Current.CancellationToken);

        Assert.Equal([photograph], SelectedOptions(RememberedSelectOf(page)));
    }

    // the island's first render, before its circuit connects
    [Fact]
    public async Task AddFromImagesStartsAtTheSavedWorkType()
    {
        var (catalog, client) = await SiteWithAdminAsync();
        var photograph = (await catalog.GetTypeIdAsync("Photograph")).Value.ToString(CultureInfo.InvariantCulture);
        await SaveAsync(client, catalog, RememberedInputs.WorkType, photograph);

        var page = await client.GetStringAsync(PageUrls.AddWorksFromImages, TestContext.Current.CancellationToken);

        Assert.Equal([photograph], SelectedOptions(page));
    }

    private static Task<string> RedirectFromAddAsync(HttpClient client) => RedirectFromAsync(client, AddPath);

    private static async Task<string> RedirectFromAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response.Headers.Location?.ToString() ?? "";
    }

    // the page's other selects, like the filters', have selected options too
    private static string RememberedSelectOf(string page) => RememberedSelect().Match(page).Value;

    [Fact]
    public async Task RefusesAnInputNameItDoesntList()
    {
        var (catalog, client) = await SiteWithAdminAsync();

        var response = await SaveAsync(client, catalog, "anything", "1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RefusesASaveWithoutTheAntiforgeryToken()
    {
        var (_, client) = await SiteWithAdminAsync();

        var response = await client.PostAsync(
            RememberedInputEndpoints.Path,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    [RememberedInputEndpoints.NameField] = RememberedInputs.WorkType,
                    [RememberedInputEndpoints.ValueField] = "1",
                }
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static List<string> SelectedOptions(string page) =>
        [.. SelectedOption().Matches(page).Select(match => match.Groups["value"].Value)];

    [GeneratedRegex("data-token-field=\"(?<field>[^\"]+)\" data-token=\"(?<token>[^\"]+)\"")]
    private static partial Regex SaveToken();

    [GeneratedRegex("<remembered-select.*?</remembered-select>", RegexOptions.Singleline)]
    private static partial Regex RememberedSelect();

    [GeneratedRegex("<option value=\"(?<value>[^\"]*)\" selected")]
    private static partial Regex SelectedOption();
}
