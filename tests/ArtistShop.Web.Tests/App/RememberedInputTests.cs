using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// the dashboard's artwork type, which comes back at the one its admin last changed it to
[Collection(TestAppCollection.Name)]
public sealed partial class RememberedInputTests(TestApp app)
{
    private const string DashboardPath = "/admin";

    private async Task<(CatalogTestData Catalog, HttpClient Client)> SiteWithAdminAsync()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        return (new CatalogTestData(database), client);
    }

    // as RememberedSelect's script sends it: with the token the page put on the element
    private static async Task<HttpResponseMessage> SaveAsync(HttpClient client, string inputName, string value)
    {
        var page = await client.GetStringAsync(DashboardPath, TestContext.Current.CancellationToken);
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
    public async Task TheDashboardComesBackAtTheSavedArtworkType()
    {
        var (catalog, client) = await SiteWithAdminAsync();
        var photograph = (await catalog.GetTypeIdAsync("Photograph")).Value.ToString(CultureInfo.InvariantCulture);

        var response = await SaveAsync(client, RememberedInputs.DashboardArtworkType, photograph);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var page = await client.GetStringAsync(DashboardPath, TestContext.Current.CancellationToken);
        Assert.Equal([photograph], SelectedOptions(page));
    }

    // a type deleted since it was saved is no option any more, so the first one shows, as for nobody
    [Fact]
    public async Task ASavedValueThePageNoLongerOffersSelectsNothing()
    {
        var site = await app.MakeSiteAsync();
        await app
            .Services.GetRequiredService<SiteMemberInputValueRepository>()
            .SetAsync(site.Id, await app.UserIdAsync(site.OwnerEmail), RememberedInputs.DashboardArtworkType, "999999");
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var page = await client.GetStringAsync(DashboardPath, TestContext.Current.CancellationToken);

        Assert.Contains("<remembered-select", page);
        Assert.Empty(SelectedOptions(page));
    }

    [Fact]
    public async Task RefusesAnInputNameItDoesntList()
    {
        var (_, client) = await SiteWithAdminAsync();

        var response = await SaveAsync(client, "anything", "1");

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
                    [RememberedInputEndpoints.NameField] = RememberedInputs.DashboardArtworkType,
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
