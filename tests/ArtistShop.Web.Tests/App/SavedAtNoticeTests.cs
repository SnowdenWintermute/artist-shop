using ArtistShop.Web.Components;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// "Saved at" beside the up link, which a layout draws on the vocabulary and work type pages
public sealed class SavedAtNoticeTests(TestApp app)
{
    [Fact]
    public async Task APageDrawsTheNoticeInItsLayoutsTopBar()
    {
        var site = await app.MakeSiteAsync();
        var database = app.DatabaseOf(site);
        var vocabularyId = await new CatalogTestData(database).AddPaintingVocabularyAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        var savedAt = new DateTimeOffset(2026, 10, 14, 16, 20, 0, TimeSpan.Zero);

        var page = await client.GetStringAsync(
            $"{PageUrls.EditVocabulary(vocabularyId)}?{SavedAtQuery.Key}={SavedAtQuery.Value(savedAt)}",
            TestContext.Current.CancellationToken
        );

        Assert.Matches("Admin Dashboard</a>\\s*<saved-notice[^>]*>Saved at <local-time><time datetime=\"2026-10-14T16:20:00Z\">16:20</time>", page);
    }
}
