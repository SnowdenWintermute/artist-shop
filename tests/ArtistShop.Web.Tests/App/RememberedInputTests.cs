using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Components.Catalog;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// Add one's artwork type: the page without a type goes to the one its admin last changed it to
[Collection(TestAppCollection.Name)]
public sealed partial class RememberedInputTests(TestApp app)
{
    private const string AddPath = "/admin/catalog/artworks/add";

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
    public async Task AddOneGoesToTheSavedArtworkType()
    {
        var (catalog, client) = await SiteWithAdminAsync();
        var photograph = (await catalog.GetTypeIdAsync("Photograph")).Value.ToString(CultureInfo.InvariantCulture);

        var response = await SaveAsync(client, catalog, RememberedInputs.AddArtworkType, photograph);

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
            .SetAsync(site.Id, await app.UserIdAsync(site.OwnerEmail), RememberedInputs.AddArtworkType, "999999");
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        var first = ArtworkTypeOrder.SortedByName(await new ArtworkTypeRepository(database).GetAllAsync())[0];

        Assert.EndsWith($"?type={first.Id.Value}", await RedirectFromAddAsync(client));
    }

    // the address decides the type, so the select shows it whatever was saved
    [Fact]
    public async Task TheSelectShowsTheTypeInTheAddress()
    {
        var (catalog, client) = await SiteWithAdminAsync();
        var photograph = (await catalog.GetTypeIdAsync("Photograph")).Value.ToString(CultureInfo.InvariantCulture);
        await SaveAsync(client, catalog, RememberedInputs.AddArtworkType, photograph);
        var painting = (await catalog.GetPaintingTypeIdAsync()).Value.ToString(CultureInfo.InvariantCulture);

        var page = await client.GetStringAsync($"{AddPath}?type={painting}", TestContext.Current.CancellationToken);

        Assert.Contains("<remembered-select", page);
        Assert.Equal([painting], SelectedOptions(page));
    }

    private static async Task<string> RedirectFromAddAsync(HttpClient client)
    {
        var response = await client.GetAsync(AddPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response.Headers.Location?.ToString() ?? "";
    }

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
                    [RememberedInputEndpoints.NameField] = RememberedInputs.AddArtworkType,
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

    [GeneratedRegex("<option value=\"(?<value>[^\"]*)\" selected")]
    private static partial Regex SelectedOption();
}
