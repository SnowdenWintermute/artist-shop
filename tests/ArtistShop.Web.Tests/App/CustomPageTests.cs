using System.Net;
using System.Text.RegularExpressions;
using ArtistShop.Web.Components;
using ArtistShop.Web.Components.CustomPages;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Sites;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// what visitors see of a website's custom pages and nav, and the admin pages that change them
[Collection(TestAppCollection.Name)]
public sealed partial class CustomPageTests(TestApp app)
{
    private CustomPageRepository PagesOf(TestSite site) =>
        new(app.Services.GetRequiredService<SiteDatabases>().For(site.Id));

    private NavEntryRepository NavOf(TestSite site) =>
        new(app.Services.GetRequiredService<SiteDatabases>().For(site.Id));

    private static async Task<IEnumerable<(string Href, string Text)>> NavLinksAsync(HttpClient client)
    {
        var page = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        return NavLink().Matches(page).Select(match => (match.Groups["href"].Value, match.Groups["text"].Value.Trim()));
    }

    [Fact]
    public async Task ANewSitesHomeShowsItsCollections()
    {
        var site = await app.MakeSiteAsync();

        var page = await app.ClientFor(site.Host).GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.Contains("<title>Home</title>", page);
        Assert.Contains($"<h1 class=\"mb-4\">{SiteWording.Default.Collection.PluralHeading}</h1>", page);
    }

    [Fact]
    public async Task TheHomePagesOwnAddressGoesToSlash()
    {
        var response = await app.ClientFor(TestApp.FirstSiteHost)
            .GetAsync("/home", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task AnAddressNoPageHasIsNotFound()
    {
        var response = await app.ClientFor(TestApp.FirstSiteHost)
            .GetAsync("/no-such-page", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // the bar's links come once in the bar and once in the drawer
    [Fact]
    public async Task ANewSitesNavIsHomeThenBlog()
    {
        var site = await app.MakeSiteAsync();

        Assert.Equal(
            [("/", "Home"), ("/posts", "Blog"), ("/", "Home"), ("/posts", "Blog")],
            await NavLinksAsync(app.ClientFor(site.Host))
        );
    }

    [Fact]
    public async Task AnotherPageMadeHomeIsShownAtSlash()
    {
        var site = await app.MakeSiteAsync();
        var pages = PagesOf(site);
        var about = await pages.AddAsync("About", new CustomPageAddress("about"));
        await pages.MakeHomeAsync(about);
        var visitor = app.ClientFor(site.Host);

        Assert.Contains("<title>About</title>", await visitor.GetStringAsync("/", TestContext.Current.CancellationToken));
        Assert.Contains("<title>Home</title>", await visitor.GetStringAsync("/home", TestContext.Current.CancellationToken));
        var response = await visitor.GetAsync("/about", TestContext.Current.CancellationToken);
        Assert.Equal("/", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task TheNavIsInItsSavedOrder()
    {
        var site = await app.MakeSiteAsync();
        var pages = PagesOf(site);
        var about = await pages.AddAsync("About", new CustomPageAddress("about"));
        var blog = await pages.GetBlogAsync();

        await NavOf(site).SetAsync([blog.Id, about]);

        Assert.Equal(
            [("/posts", "Blog"), ("/about", "About"), ("/posts", "Blog"), ("/about", "About")],
            await NavLinksAsync(app.ClientFor(site.Host))
        );
    }

    [Fact]
    public async Task AnAddedPageOpensInItsEditor()
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await TestApp.PostFormAsync(
            client,
            PageUrls.CustomPageList,
            "add-custom-page",
            new() { ["Input.Name"] = "About me" }
        );

        var added = Assert.IsType<CustomPage>(await PagesOf(site).GetByAddressAsync(new CustomPageAddress("about-me")));
        Assert.Equal(PageUrls.EditCustomPage(added.Id), response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task APageCantHaveOneOfTheWebsitesOwnAddresses()
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await TestApp.PostFormAsync(
            client,
            PageUrls.CustomPageList,
            "add-custom-page",
            new() { ["Input.Name"] = "Admin" }
        );

        Assert.Contains("/admin is one of the website", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Null(await PagesOf(site).GetByAddressAsync(new CustomPageAddress("admin")));
    }

    [Fact]
    public async Task AChangedAddressIsWhereThePageIs()
    {
        var site = await app.MakeSiteAsync();
        var id = await PagesOf(site).AddAsync("About", new CustomPageAddress("about"));
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(
            client,
            PageUrls.EditCustomPage(id),
            "custom-page-details",
            new() { ["Input.Name"] = "About me", ["Input.Address"] = "me" }
        );

        var visitor = app.ClientFor(site.Host);
        Assert.Contains("<title>About me</title>", await visitor.GetStringAsync("/me", TestContext.Current.CancellationToken));
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await visitor.GetAsync("/about", TestContext.Current.CancellationToken)).StatusCode
        );
    }

    [Fact]
    public async Task ADeletedPageLeavesTheNav()
    {
        var site = await app.MakeSiteAsync();
        var pages = PagesOf(site);
        var about = await pages.AddAsync("About", new CustomPageAddress("about"));
        await NavOf(site).SetAsync([about]);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(client, PageUrls.EditCustomPage(about), "delete-custom-page", []);

        Assert.Empty(await NavLinksAsync(app.ClientFor(site.Host)));
        Assert.Null(await pages.GetAsync(about));
    }

    // NavItem's links, the only ones in the heading font, whatever order NavLink writes their attributes in
    [Fact]
    public async Task TheBlogPageCanBeHomeAndRenamed()
    {
        var site = await app.MakeSiteAsync();
        var pages = PagesOf(site);
        var blog = await pages.GetBlogAsync();
        await pages.UpdateAsync(blog.Id, "Journal", blog.Address);
        await pages.MakeHomeAsync(blog.Id);

        var home = await app.ClientFor(site.Host).GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.Contains("<title>Journal</title>", home);
        Assert.Contains("No posts yet.", home);
    }

    // the up link goes wherever the blog page is, by its name
    [Fact]
    public async Task APostLinksUpToTheBlogPage()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var pages = PagesOf(site);
        var blog = await pages.GetBlogAsync();
        await pages.UpdateAsync(blog.Id, "Journal", new CustomPageAddress("journal"));
        await new PostRepository(database).AddAsync(
            new PostTitle("Spring"),
            PostSlug.FromTitle("Spring"),
            new PostBody("""{"ops":[{"insert":"Hello\n"}]}"""),
            PostStatus.Published
        );

        var post = await app.ClientFor(site.Host).GetStringAsync("/posts/spring", TestContext.Current.CancellationToken);

        Assert.Matches("""<a [^>]*href="/journal"[^>]*>[^<]*Journal""", post);
    }

    // what the row editor makes: two slots side by side, both drawn on the page
    [Fact]
    public async Task ARowShowsEachSlotsComponents()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var pageId = await PagesOf(site).AddAsync("Both", new CustomPageAddress("both"));
        var rowId = await new PageRowRepository(database).AddAsync(pageId);
        await new RowSlotRepository(database).AddAsync(rowId, slotCount: 1);
        var slots = Assert.Single((await PagesOf(site).GetLayoutAsync(pageId)).Rows).Slots;
        var components = new CustomComponentRepository(database);
        await components.AddToSlotAsync(slots[0].Id, "Viewer", new CollectionViewerSettings());
        await components.AddToSlotAsync(slots[1].Id, "Posts", new PostListSettings());

        var page = await app.ClientFor(site.Host).GetStringAsync("/both", TestContext.Current.CancellationToken);

        Assert.Contains($">{SiteWording.Default.Collection.PluralHeading}</h1>", page);
        Assert.Contains("No posts yet.", page);
    }

    [Fact]
    public async Task ANewRowOpensOnAddingAComponent()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var home = await PagesOf(site).GetHomeAsync();
        var rowId = await new PageRowRepository(database).AddAsync(home.Id);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var editor = await client.GetStringAsync(PageUrls.EditNewPageRow(home.Id, rowId), TestContext.Current.CancellationToken);

        Assert.Contains("Add a component", editor);
        Assert.Contains("Post list", editor);
    }

    // the preview takes the theme visitors see, not the admin's
    [Fact]
    public async Task TheRowPreviewIsInTheWebsitesTheme()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        await new ThemeRepository(database).UseAsync(new ThemeKey.Preset(ThemePreset.Dark));
        var home = await PagesOf(site).GetHomeAsync();
        var row = Assert.Single((await PagesOf(site).GetLayoutAsync(home.Id)).Rows);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var editor = await client.GetStringAsync(PageUrls.EditPageRow(home.Id, row.Id), TestContext.Current.CancellationToken);

        var page = ThemePresets.For(ThemePreset.Dark).Theme.Colors.Resolve(ColorRole.Page).Hex;
        Assert.Matches($"<style>\\[data-row-preview\\] \\{{[^}}]*--theme-page: {page};", editor);
    }

    [Fact]
    public async Task AnArticleIsDrawnAtItsWidth()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var pageId = await PagesOf(site).AddAsync("About", new CustomPageAddress("about"));
        await new PageRowRepository(database).AddAsync(pageId);
        var slot = Assert.Single(Assert.Single((await PagesOf(site).GetLayoutAsync(pageId)).Rows).Slots);
        await new CustomComponentRepository(database).AddToSlotAsync(
            slot.Id,
            "Statement",
            new ArticleSettings(new PostBody("""{"ops":[{"insert":"I paint rooms.\n"}]}"""), ArticleWidth.Narrow)
        );

        var page = await app.ClientFor(site.Host).GetStringAsync("/about", TestContext.Current.CancellationToken);

        Assert.Contains("I paint rooms.", page);
        Assert.Contains("max-w-[42rem]", page);
        Assert.DoesNotContain("Statement", page);
    }

    [Fact]
    public async Task AComponentIsRenamedOnItsSettingsPage()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var home = await PagesOf(site).GetHomeAsync();
        var row = Assert.Single((await PagesOf(site).GetLayoutAsync(home.Id)).Rows);
        var component = Assert.Single(Assert.Single(row.Slots).Stack);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(
            client,
            PageUrls.EditCustomComponent(component.Id, home.Id, row.Id),
            "custom-component",
            new() { ["Input.Name"] = "Front grid" }
        );

        var saved = Assert.IsType<CustomComponentInUse>(await new CustomComponentRepository(database).GetAsync(component.Id));
        Assert.Equal("Front grid", saved.Component.Name);
        Assert.IsType<CollectionViewerSettings>(saved.Component.Settings);
    }

    [Fact]
    public async Task AnArticlesSettingsPageHasThePostEditor()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var home = await PagesOf(site).GetHomeAsync();
        var row = Assert.Single((await PagesOf(site).GetLayoutAsync(home.Id)).Rows);
        var id = await new CustomComponentRepository(database).AddToSlotAsync(
            Assert.Single(row.Slots).Id,
            "Intro",
            new ArticleSettings(PostBody.Empty, ArticleWidth.Narrow)
        );
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var settings = await client.GetStringAsync(PageUrls.EditCustomComponent(id, home.Id, row.Id), TestContext.Current.CancellationToken);

        Assert.Contains("<post-body-editor", settings);
        Assert.Contains("data-backup-key=\"component-", settings);
        Assert.Contains("Row on Home", settings);
    }

    [Fact]
    public void ANewComponentsNameSkipsTakenNumbers() =>
        Assert.Equal(
            ["Article 2", "Article 4"],
            CustomComponentNames.NewNames(CustomComponentType.Article, SiteWording.Default, new HashSet<string> { "Article 1", "Article 3" }).Take(2)
        );

    [GeneratedRegex("""<a (?=[^>]*class="font-heading)[^>]*href="(?<href>[^"]*)"[^>]*>(?<text>[^<]*)</a>""")]
    private static partial Regex NavLink();
}
