using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Sites;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// the example vocabularies a new site starts with
[Collection(TestAppCollection.Name)]
public sealed class StarterVocabularyTests(TestApp app)
{
    [Fact]
    public async Task ANewSiteHasMediumAndSupportForPaintings()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var painting = (await new WorkTypeRepository(database).GetAllAsync()).Single(type => type.Name.Value == "Painting");
        var vocabularies = new VocabularyRepository(database);
        var terms = new VocabularyTermRepository(database);

        var found = new Dictionary<string, string[]>();
        foreach (var vocabulary in await vocabularies.GetAllAsync())
        {
            var withTypes = await vocabularies.GetAsync(vocabulary.Id);
            Assert.Equal([painting.Id], withTypes?.WorkTypeIds);
            found[vocabulary.Name.Value] = [.. (await terms.GetAllWithUsageAsync(vocabulary.Id)).Select(term => term.Name.Value).Order()];
        }

        Assert.Equal(
            new Dictionary<string, string[]>
            {
                ["Medium"] = ["Acrylic", "Oil", "Watercolor"],
                ["Support"] = ["Canvas", "Paper"],
            },
            found
        );
    }
}
