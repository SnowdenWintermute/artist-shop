using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

[Collection(TestAppCollection.Name)]
public sealed class AddWorkPageTests(TestApp app)
{
    // Firefox lets letters into a type="number" box and sends it empty, so the year is a text box:
    // a typo comes back with an error rather than saving the work without a year
    [Fact]
    public async Task AYearThatIsNotANumberComesBackAsTypedWithAnError()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));
        var typeId = await new CatalogTestData(database).GetPaintingTypeIdAsync();

        var response = await TestApp.PostFormAsync(
            client,
            $"/admin/catalog/works/add?type={typeId.Value}",
            "new-work",
            new() { ["Input.Name"] = "Dawn", ["Input.YearCreated"] = "19a5" }
        );

        var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("value=\"19a5\"", page);
        // the form's own message for a value it couldn't read, which names the typed text
        Assert.Matches("text-red-400\">[^<]*19a5", page);
        Assert.Empty(await new WorkRepository(database).GetAllAsync());
    }
}
