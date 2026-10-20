using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using ArtistShop.Web.Components;
using ArtistShop.Web.Components.CustomPages;
using ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;
using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// what visitors see of a website's custom pages and nav, and the admin pages that change them
public sealed partial class CustomPageTests(TestApp app)
{
    private CustomPageRepository PagesOf(TestSite site) =>
        new(app.Services.GetRequiredService<SiteDatabases>().For(site.Id));

    private PageRowRepository RowsOf(TestSite site) =>
        new(app.Services.GetRequiredService<SiteDatabases>().For(site.Id));

    private UploadedImageRepository LibraryOf(TestSite site) =>
        new(app.Services.GetRequiredService<SiteDatabases>().For(site.Id));

    private SlideRepository SlidesOf(TestSite site) =>
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

    // the home page's one row holds the collections, under their heading
    [Fact]
    public async Task APagesOnlyRowSitsAboveTheFooter()
    {
        var site = await app.MakeSiteAsync();

        var page = await app.ClientFor(site.Host).GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.Contains("<main data-custom-page></main>", page);
        Assert.True(page.IndexOf("data-last-row") < page.IndexOf(SiteWording.Default.Collection.PluralHeading));
    }

    // the first row joins the nav and the last the footer, leaving <main> empty
    [Fact]
    public async Task AFirstAndLastRowFillingTheScreenSitBesideTheNavAndTheFooter()
    {
        var site = await app.MakeSiteAsync();
        var home = Assert.IsType<CustomPage>(await PagesOf(site).GetHomeAsync());
        var first = Assert.Single((await PagesOf(site).GetLayoutAsync(home.Id)).Rows);
        var last = await RowsOf(site).AddAsync(home.Id);
        await RowsOf(site).SetSettingsAsync(first.Id, first.Settings with { Height = RowHeight.FillScreen });
        await RowsOf(site).SetSettingsAsync(last, first.Settings with { Height = RowHeight.FillScreen });

        var page = await app.ClientFor(site.Host).GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.Contains("<main data-custom-page></main>", page);
        Assert.True(page.IndexOf("data-below-nav") < page.IndexOf(SiteWording.Default.Collection.PluralHeading));
        Assert.True(page.IndexOf(SiteWording.Default.Collection.PluralHeading) < page.IndexOf("data-above-footer"));
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
            new RichText("""{"ops":[{"insert":"Hello\n"}]}"""),
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
        await components.AddToSlotAsync(slots[0].Id, "Viewer", CardGridSettings.AllCollections);
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

    // a background colour chosen just now has none posted, so it starts as the row's page colour, Dark's here
    [Fact]
    public async Task TheRowSettingsFormSavesEveryChoice()
    {
        var site = await app.MakeSiteAsync();
        var home = await PagesOf(site).GetHomeAsync();
        var row = Assert.Single((await PagesOf(site).GetLayoutAsync(home.Id)).Rows);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(
            client,
            PageUrls.EditPageRow(home.Id, row.Id),
            "row-settings",
            new()
            {
                ["Input.Height"] = nameof(RowHeight.FillScreen),
                ["Input.ContentWidth"] = nameof(RowContentWidth.EdgeToEdge),
                ["Input.TopPadding"] = nameof(RowVerticalPadding.None),
                ["Input.BottomPadding"] = nameof(RowVerticalPadding.Large),
                ["Input.SidePadding"] = nameof(RowSidePadding.Small),
                ["Input.Gap"] = nameof(RowGap.None),
                ["Input.Alignment"] = nameof(RowAlignment.Center),
                ["Input.Theme"] = new ThemeKey.Preset(ThemePreset.Dark).QueryValue,
                ["Input.BackgroundType"] = nameof(RowBackgroundType.Color),
            }
        );

        Assert.Equal(
            new RowSettings(
                RowHeight.FillScreen,
                RowContentWidth.EdgeToEdge,
                new RowPadding(RowVerticalPadding.None, RowVerticalPadding.Large, RowSidePadding.Small),
                RowGap.None,
                RowAlignment.Center,
                new ThemeKey.Preset(ThemePreset.Dark),
                new RowBackground.Color(ThemePresets.For(ThemePreset.Dark).Theme.Colors.Resolve(ColorRole.Page))
            ),
            Assert.Single((await PagesOf(site).GetLayoutAsync(home.Id)).Rows).Settings
        );
        // and the editor draws them, colour and all
        await client.GetStringAsync(PageUrls.EditPageRow(home.Id, row.Id), TestContext.Current.CancellationToken);
    }

    // the row settings form as a new row posts it, with these fields added or changed
    private static Dictionary<string, string> RowFormWith(Dictionary<string, string> changes) =>
        new Dictionary<string, string>
        {
            ["Input.Height"] = nameof(RowHeight.Natural),
            ["Input.ContentWidth"] = nameof(RowContentWidth.Page),
            ["Input.TopPadding"] = nameof(RowVerticalPadding.Auto),
            ["Input.BottomPadding"] = nameof(RowVerticalPadding.Auto),
            ["Input.SidePadding"] = nameof(RowSidePadding.Medium),
            ["Input.Gap"] = nameof(RowGap.Medium),
            ["Input.Alignment"] = nameof(RowAlignment.Start),
            ["Input.Theme"] = RowSettingsForm.WebsiteTheme,
            ["Input.BackgroundType"] = nameof(RowBackgroundType.None),
        }
            .Concat(changes)
            .GroupBy(field => field.Key)
            .ToDictionary(fields => fields.Key, fields => fields.Last().Value);

    [Fact]
    public async Task AnImageBackgroundIsSavedFromTheRowFormAndDrawnBehindTheRow()
    {
        var site = await app.MakeSiteAsync();
        var home = await PagesOf(site).GetHomeAsync();
        var row = Assert.Single((await PagesOf(site).GetLayoutAsync(home.Id)).Rows);
        var storageKey = await UploadedImageTestData.AddAsync(LibraryOf(site), "pier.jpg", "A pier at dusk");
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(
            client,
            PageUrls.EditPageRow(home.Id, row.Id),
            "row-settings",
            RowFormWith(new()
            {
                ["Input.BackgroundType"] = nameof(RowBackgroundType.Image),
                ["Input.ImageStorageKey"] = storageKey,
                ["Input.ImageWorkId"] = "",
                ["Input.BackgroundFit"] = nameof(ImageFit.Contain),
                ["Input.HasColorBehindImage"] = "False",
            })
        );

        Assert.Equal(
            new RowBackground.Image(new ImageSource.Library(storageKey), ImageFit.Contain, Behind: null),
            Assert.Single((await PagesOf(site).GetLayoutAsync(home.Id)).Rows).Background
        );
        var page = await app.ClientFor(site.Host).GetStringAsync("/", TestContext.Current.CancellationToken);
        Assert.Contains("data-test=\"row-background-image\"", page);
        Assert.Contains(storageKey, await client.GetStringAsync(PageUrls.EditPageRow(home.Id, row.Id), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ASlideshowBackgroundShowsItsFirstSlideAndHoldsTheRestBackWithAPauseButton()
    {
        var site = await app.MakeSiteAsync();
        var home = await PagesOf(site).GetHomeAsync();
        var row = Assert.Single((await PagesOf(site).GetLayoutAsync(home.Id)).Rows);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        var editorUrl = PageUrls.EditPageRow(home.Id, row.Id);

        await TestApp.PostFormAsync(client, editorUrl, "row-settings", RowFormWith(new()
        {
            ["Input.BackgroundType"] = nameof(RowBackgroundType.Slideshow),
            ["Input.SlideSource"] = nameof(SlideSourceType.ChosenSlides),
            ["Input.SlideInterval"] = nameof(SlideshowInterval.FifteenSeconds),
            ["Input.BackgroundFit"] = nameof(ImageFit.Contain),
            ["Input.HasColorBehindImage"] = "False",
        }));

        Assert.Equal(
            new RowBackground.Slideshow(new SlideSource.ChosenSlides(), SlideshowInterval.FifteenSeconds, ImageFit.Contain, Behind: null),
            Assert.Single((await PagesOf(site).GetLayoutAsync(home.Id)).Rows).Background
        );

        foreach (var name in new[] { "pier.jpg", "dune.jpg", "cliff.jpg" })
        {
            var storageKey = await UploadedImageTestData.AddAsync(LibraryOf(site), name, name);
            await TestApp.PostFormAsync(client, editorUrl, "add-slide", new() { ["Slide.ImageStorageKey"] = storageKey });
        }

        Assert.Equal(3, (await SlidesOf(site).GetAsync(new SlideOwner.Row(row.Id))).Count);
        var page = await app.ClientFor(site.Host).GetStringAsync("/", TestContext.Current.CancellationToken);
        var waiting = page.Split("<template data-part=\"later\">").Length - 1;
        Assert.Equal((3, 2), (page.Split("data-test=\"row-background-slide\"").Length - 1, waiting));
        Assert.Contains($"data-pauses=\"{RowBackgroundLayer.SlideshowIdOf(row.Id)}\"", page);
    }

    // the colour input posts the colour the row had, which stays behind the image
    [Fact]
    public async Task AColourBackgroundSwitchedToAnImageKeepsItsColourBehindIt()
    {
        var site = await app.MakeSiteAsync();
        var home = await PagesOf(site).GetHomeAsync();
        var row = Assert.Single((await PagesOf(site).GetLayoutAsync(home.Id)).Rows);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(
            client,
            PageUrls.EditPageRow(home.Id, row.Id),
            "row-settings",
            RowFormWith(new() { ["Input.BackgroundType"] = nameof(RowBackgroundType.Image), ["Input.BackgroundColor"] = "#123456" })
        );

        Assert.Equal(
            new RowBackground.Image(Source: null, ImageFit.Cover, RgbColor.Parse("#123456")),
            Assert.Single((await PagesOf(site).GetLayoutAsync(home.Id)).Rows).Background
        );
    }

    // the preview is the public page itself, so in the theme visitors see, with the row being edited outlined
    [Fact]
    public async Task TheRowEditorPreviewsItsPage()
    {
        var site = await app.MakeSiteAsync();
        var home = await PagesOf(site).GetHomeAsync();
        var row = Assert.Single((await PagesOf(site).GetLayoutAsync(home.Id)).Rows);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var editor = await client.GetStringAsync(PageUrls.EditPageRow(home.Id, row.Id), TestContext.Current.CancellationToken);

        Assert.Matches($"<page-preview[^>]* data-row-id=\"{row.Id.Value}\"", editor);
        Assert.Matches($"<iframe[^>]* src=\"{Regex.Escape(PageUrls.CustomPage(home))}\"[^>]* data-test=\"page-preview\"", editor);
    }

    // the outline is on every editor of the page, with each row's and component's way into its editor
    [Fact]
    public async Task TheComponentEditorOutlinesItsPage()
    {
        var site = await app.MakeSiteAsync();
        var home = await PagesOf(site).GetHomeAsync();
        var row = Assert.Single((await PagesOf(site).GetLayoutAsync(home.Id)).Rows);
        var component = Assert.Single(Assert.Single(row.Slots).Stack);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        var componentHref = PageUrls.EditCustomComponent(component.Id, home.Id, row.Id);

        var editor = await client.GetStringAsync(componentHref, TestContext.Current.CancellationToken);

        // the outline's own Edit links, as the up link goes to the row editor too
        Assert.Matches($"<a href=\"{Regex.Escape(PageUrls.EditPageRow(home.Id, row.Id))}\"[^>]* data-test=\"outline-edit\"", editor);
        Assert.Matches($"<a href=\"{Regex.Escape(HtmlEncoder.Default.Encode(componentHref))}\"[^>]* data-test=\"outline-edit\"", editor);
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
            new TextSettings(new RichText("""{"ops":[{"insert":"I paint rooms.\n"}]}"""), TextWidth.Narrow)
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
            new()
            {
                ["Input.Name"] = "Front grid",
                ["Input.Source"] = nameof(CardGridSourceType.AllCollections),
                ["Input.HeadingType"] = nameof(CardGridHeadingType.Automatic),
                ["Input.CardsPerPage"] = "24",
                ["Input.HasPageLinks"] = "true",
                ["Input.Layout"] = nameof(CardGridLayout.EvenGrid),
            }
        );

        var saved = Assert.IsType<CustomComponentInUse>(await new CustomComponentRepository(database).GetAsync(component.Id));
        Assert.Equal("Front grid", saved.Component.Name);
        Assert.Equal(CardGridSettings.AllCollections, saved.Component.Settings);
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
            new TextSettings(RichText.Empty, TextWidth.Narrow)
        );
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var settings = await client.GetStringAsync(PageUrls.EditCustomComponent(id, home.Id, row.Id), TestContext.Current.CancellationToken);

        Assert.Contains("<rich-text-editor", settings);
        Assert.Contains("data-backup-key=\"component-", settings);
        Assert.Contains("Row on Home", settings);
    }

    // the preview's post list reads the address too, and once took the blog page's id for a page of posts
    // that didn't exist, and redirected
    [Fact]
    public async Task APostListsSettingsPageKeepsTheRowItWasOpenedFrom()
    {
        var site = await app.MakeSiteAsync();
        var blog = await PagesOf(site).GetBlogAsync();
        var row = Assert.Single((await PagesOf(site).GetLayoutAsync(blog.Id)).Rows);
        var component = Assert.Single(Assert.Single(row.Slots).Stack);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var settings = await client.GetStringAsync(PageUrls.EditCustomComponent(component.Id, blog.Id, row.Id), TestContext.Current.CancellationToken);

        Assert.Contains("Row on Blog", settings);
    }

    // so a post's Up link always leads to its posts
    [Fact]
    public async Task TheBlogPagesOnlyPostListCantBeTakenOff()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var pages = PagesOf(site);
        var blog = await pages.GetBlogAsync();
        var row = Assert.Single((await pages.GetLayoutAsync(blog.Id)).Rows);
        var slot = Assert.Single(row.Slots);
        var postList = Assert.Single(slot.Stack);
        var components = new CustomComponentRepository(database);
        var slots = new RowSlotRepository(database);
        await slots.AddAsync(row.Id, slotCount: 1);

        Assert.True(await components.IsBlogPagesOnlyPostListAsync(postList.Id));
        await Assert.ThrowsAsync<BlogPageNeedsPostListException>(() => components.DeleteAsync(postList.Id));
        await Assert.ThrowsAsync<BlogPageNeedsPostListException>(() => slots.RemovePlacementAsync(slot.Id, postList.Id));
        await Assert.ThrowsAsync<BlogPageNeedsPostListException>(() => slots.DeleteAsync(row.Id, slot.Id, slotCount: 2));
        await Assert.ThrowsAsync<BlogPageNeedsPostListException>(() => new PageRowRepository(database).DeleteAsync(row.Id));
        Assert.Equal([[postList.Id], []], Assert.Single((await pages.GetLayoutAsync(blog.Id)).Rows).Slots.Select(kept => kept.Stack.Select(component => component.Id)));
    }

    [Fact]
    public async Task ABlogPagePostListCanGoWhileAnotherStays()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var blog = await PagesOf(site).GetBlogAsync();
        var slot = Assert.Single(Assert.Single((await PagesOf(site).GetLayoutAsync(blog.Id)).Rows).Slots);
        var components = new CustomComponentRepository(database);
        var other = await components.AddToSlotAsync(slot.Id, "Recent", new PostListSettings());

        Assert.False(await components.IsBlogPagesOnlyPostListAsync(Assert.Single(slot.Stack).Id));
        await components.DeleteAsync(Assert.Single(slot.Stack).Id);

        Assert.True(await components.IsBlogPagesOnlyPostListAsync(other));
    }

    [Fact]
    public async Task APageWithoutAPostListCantBeMadeTheBlogPage()
    {
        var site = await app.MakeSiteAsync();
        var pages = PagesOf(site);
        var blog = await pages.GetBlogAsync();
        var home = await pages.GetHomeAsync();

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => pages.MakeBlogAsync(home.Id));

        Assert.Equal(blog.Id, (await pages.GetBlogAsync()).Id);
    }

    [Fact]
    public async Task TheBlogPagesPostListSaysWhyItCantBeDeleted()
    {
        var site = await app.MakeSiteAsync();
        var blog = await PagesOf(site).GetBlogAsync();
        var row = Assert.Single((await PagesOf(site).GetLayoutAsync(blog.Id)).Rows);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var settings = await client.GetStringAsync(
            PageUrls.EditCustomComponent(Assert.Single(Assert.Single(row.Slots).Stack).Id, blog.Id, row.Id),
            TestContext.Current.CancellationToken
        );

        Assert.Contains("Make another page the blog page first.", settings);
        Assert.DoesNotContain("Delete component", settings);
    }

    [Fact]
    public async Task AComponentIsDeletedFromItsSettingsPage()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var home = await PagesOf(site).GetHomeAsync();
        var row = Assert.Single((await PagesOf(site).GetLayoutAsync(home.Id)).Rows);
        var component = Assert.Single(Assert.Single(row.Slots).Stack);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        var path = PageUrls.EditCustomComponent(component.Id, home.Id, row.Id);

        var settings = await client.GetStringAsync(path, TestContext.Current.CancellationToken);
        var response = await TestApp.PostFormAsync(client, path, "delete-custom-component", []);

        Assert.Contains("It will be taken off Home.", settings);
        Assert.Equal(PageUrls.EditPageRow(home.Id, row.Id), response.Headers.Location?.AbsolutePath);
        Assert.Null(await new CustomComponentRepository(database).GetAsync(component.Id));
    }

    [Fact]
    public void ANewComponentsNameSkipsTakenNumbers()
    {
        var name = CustomComponentNames.Of(CustomComponentStarter.Text, SiteWording.Default);

        Assert.Equal(
            [$"{name} 2", $"{name} 4"],
            CustomComponentNames.NewNames(CustomComponentStarter.Text, SiteWording.Default, new HashSet<string> { $"{name} 1", $"{name} 3" }).Take(2)
        );
    }

    [Fact]
    public void AHeadingIsATextComponentStartingWithOneLargeHeading()
    {
        var text = Assert.IsType<TextSettings>(NewCustomComponentSettings.For(CustomComponentStarter.Heading));
        var heading = Assert.IsType<HeadingBlock>(Assert.Single(RichTextParser.Parse(text.Body).Blocks));

        Assert.Equal(HeadingLevel.One, heading.Level);
        Assert.Equal("Heading", Assert.Single(heading.Text).Text);
        Assert.Equal("Heading 1", CustomComponentNames.NewNames(CustomComponentStarter.Heading, SiteWording.Default, new HashSet<string>()).First());
    }

    [GeneratedRegex("""<a (?=[^>]*class="font-heading)[^>]*href="(?<href>[^"]*)"[^>]*>(?<text>[^<]*)</a>""")]
    private static partial Regex NavLink();
}
