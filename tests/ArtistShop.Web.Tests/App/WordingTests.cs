using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using ArtistShop.Web.Components;
using ArtistShop.Web.Components.Pages.Admin.Publishing.Posts.WorkPicker;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// What a website calls collections and works, chosen on the Wording page, as visitors and admins see it
[Collection(TestAppCollection.Name)]
public sealed partial class WordingTests(TestApp app)
{
    // one collection holding one photographed work: the least that puts a card on the home page
    private async Task<(TestSite Site, string CollectionPath, string WorkPath)> SiteWithAWorkAsync()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var slug = CollectionSlug.FromName("Gardens");
        var collectionId = await new CollectionRepository(database).AddAsync(new CollectionName("Gardens"), slug);
        var work = await new CatalogTestData(database).AddPaintingAsync(
            "Dawn",
            termIds: [],
            collectionIds: [collectionId],
            images: [CatalogTestData.CreateTestImage()]
        );

        return (site, PageUrls.Collection(slug), $"/works/{work.Slug.Value}");
    }

    private static readonly Dictionary<string, string> MadeUpWords = new()
    {
        ["Input.CollectionSingular"] = "Zorb",
        ["Input.CollectionPlural"] = "Zorbs",
        ["Input.WorkSingular"] = "Quux",
        ["Input.WorkPlural"] = "Quuxes",
    };

    private static Task<string> PageAsync(HttpClient client, string path) =>
        client.GetStringAsync(path, TestContext.Current.CancellationToken);

    private async Task SaveAsync(TestSite site, Dictionary<string, string> fields)
    {
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        await TestApp.PostFormAsync(client, PageUrls.Wording, "wording", fields);
    }

    private Task<SiteWording> SavedWordingAsync(TestSite site) =>
        new WordingRepository(app.Services.GetRequiredService<SiteDatabases>().For(site.Id)).GetAsync();

    [Fact]
    public async Task ANewWebsiteSaysCollectionsAndWorks()
    {
        var (site, collectionPath, workPath) = await SiteWithAWorkAsync();
        var visitor = app.ClientFor(site.Host);

        var home = await PageAsync(visitor, "/");
        Assert.Contains(">Collections</h1>", home);
        Assert.Contains("1 work", home);
        Assert.Contains("All collections", await PageAsync(visitor, collectionPath));
        Assert.Contains(">Collections</dt>", await PageAsync(visitor, workPath));
    }

    [Fact]
    public async Task SavedWordsReplaceThemOnThePublicPages()
    {
        var (site, collectionPath, workPath) = await SiteWithAWorkAsync();

        await SaveAsync(
            site,
            new()
            {
                ["Input.CollectionSingular"] = "Project",
                ["Input.CollectionPlural"] = "Projects",
                ["Input.WorkSingular"] = "Piece",
                ["Input.WorkPlural"] = "Pieces",
            }
        );

        var visitor = app.ClientFor(site.Host);
        var home = await PageAsync(visitor, "/");
        Assert.Contains(">Projects</h1>", home);
        Assert.Contains("1 piece", home);
        Assert.Contains("All projects", await PageAsync(visitor, collectionPath));
        Assert.Contains(">Projects</dt>", await PageAsync(visitor, workPath));
    }

    [Fact]
    public async Task KeptCapitalsStayInsideASentence()
    {
        var (site, collectionPath, _) = await SiteWithAWorkAsync();

        await SaveAsync(
            site,
            new()
            {
                ["Input.CollectionSingular"] = "NFT drop",
                ["Input.CollectionPlural"] = "NFT drops",
                ["Input.CollectionKeepsCase"] = "true",
            }
        );

        Assert.Contains("All NFT drops", await PageAsync(app.ClientFor(site.Host), collectionPath));
    }

    // a plural is never guessed from a singular
    [Fact]
    public async Task OneFormWithoutTheOtherIsRefusedAndNothingIsSaved()
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await TestApp.PostFormAsync(client, PageUrls.Wording, "wording", new() { ["Input.WorkSingular"] = "Piece" });

        Assert.Contains("Fill in the plural too", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(SiteWording.Default, await SavedWordingAsync(site));
    }

    // Guards every public page against a noun written into it: with words no page could hold by
    // chance, none of the default or internal names is left in anything a visitor reads
    [Fact]
    public async Task NoPublicPageSaysAWordTheArtistRenamed()
    {
        var (site, collectionPath, workPath) = await SiteWithAWorkAsync();
        await SaveAsync(site, MadeUpWords);
        var visitor = app.ClientFor(site.Host);
        // so the check below can't pass by finding no page at all
        Assert.Contains("Zorbs", VisibleText(await PageAsync(visitor, "/")));

        foreach (var path in new[] { "/", collectionPath, workPath, PageUrls.Blog })
        {
            var text = VisibleText(await PageAsync(visitor, path));

            Assert.Empty(RenamedWord().Matches(text).Select(match => $"{path}: {match.Value}"));
        }
    }

    // The same guard over the admin pages, every one the app routes to under /admin: a new page
    // with an id in its address fails here until it's given one below. Only what a page shows as it
    // loads is checked, not dialogs opened later. The Wording page names the concepts themselves,
    // so it says "collections" and "works" on purpose
    [Fact]
    public async Task NoAdminPageSaysAWordTheArtistRenamed()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var catalog = new CatalogTestData(database);
        var typeId = await catalog.GetPaintingTypeIdAsync();
        // not AddCollectionAsync, whose name says "Collection"
        var collectionId = await new CollectionRepository(database).AddAsync(new CollectionName("Gardens"), CollectionSlug.FromName("Gardens"));
        var vocabularyId = await catalog.AddPaintingVocabularyAsync();
        var termId = await catalog.AddTermAsync(vocabularyId);
        var work = await catalog.AddPaintingAsync(
            "Dawn",
            termIds: [termId],
            collectionIds: [collectionId],
            images: [CatalogTestData.CreateTestImage(), CatalogTestData.CreateTestImage()]
        );
        var postId = await new PostRepository(database).AddAsync(
            new PostTitle("Spring"),
            PostSlug.FromTitle("Spring"),
            new PostBody("""{"ops":[{"insert":"Hello\n"}]}"""),
            PostStatus.Draft
        );
        await SaveAsync(site, MadeUpWords);
        var owner = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var pathsWithIds = new Dictionary<string, string>
        {
            ["/admin/catalog/collections/{Id:int}"] = PageUrls.EditCollection(collectionId),
            ["/admin/catalog/collections/{Id:int}/add-works"] = PageUrls.AddCollectionWorks(collectionId),
            ["/admin/catalog/types/{Id:int}/edit"] = PageUrls.EditWorkType(typeId),
            ["/admin/catalog/vocabularies/{Id:int}"] = PageUrls.VocabularyTerms(vocabularyId),
            ["/admin/catalog/vocabularies/{Id:int}/edit"] = PageUrls.EditVocabulary(vocabularyId),
            ["/admin/catalog/works/{Id:int}/edit"] = PageUrls.EditWork(work.Id),
            ["/admin/posts/pick-work/{Id:int}"] = PageUrls.WorkPickerImages(work.Id, new WorkPickerTrail(WorkPickerMode.Add, null)),
            ["/admin/posts/{Id:int}/edit"] = PageUrls.EditPost(postId),
        };
        var routes = typeof(PageUrls).Assembly.GetTypes()
            .SelectMany(type => type.GetCustomAttributes<RouteAttribute>())
            .Select(route => route.Template)
            .Where(template => template.StartsWith("/admin", StringComparison.Ordinal) && template != PageUrls.Wording)
            .Distinct()
            .ToList();
        Assert.Empty(routes.Where(template => template.Contains('{') && !pathsWithIds.ContainsKey(template)));

        // the pages that ask for a type first, with one chosen, which shows the rest of the page
        var withType = $"?type={typeId.Value}";
        string[] paths =
        [
            .. routes.Select(template => pathsWithIds.GetValueOrDefault(template, template)),
            PageUrls.AddWork + withType,
            PageUrls.WorkImport + withType,
            PageUrls.WorkTable + withType,
            PageUrls.AddWorksFromImages + withType,
            PageUrls.WorkBulkImageUpload(typeId),
        ];
        // so the check below can't pass by finding no page at all
        Assert.Contains("Quuxes", VisibleText(await PageAsync(owner, PageUrls.AdminDashboard)));

        var found = new List<string>();
        foreach (var path in paths)
        {
            var text = VisibleText(await PageAfterRedirectsAsync(owner, path));
            found.AddRange(RenamedWord().Matches(text).Select(match => $"{path}: {Around(text, match)}"));
        }

        // each on its own line, as Assert.Empty would cut the list short
        Assert.True(found.Count == 0, string.Join("\n", found));
    }

    // some pages send the admin on, like a new post to its editor
    private static async Task<string> PageAfterRedirectsAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        for (var step = 0; step < 3 && response.Headers.Location is { } location; step++)
        {
            response = await client.GetAsync(location, TestContext.Current.CancellationToken);
        }

        return await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // the word with a little of the text either side, to find it on the page by
    private static string Around(string text, Match match)
    {
        var start = Math.Max(0, match.Index - 30);
        return text[start..Math.Min(text.Length, match.Index + match.Length + 30)].ReplaceLineEndings(" ");
    }

    // the text and the labels a visitor's browser shows or reads aloud, without scripts, styles,
    // comments or tags, nor the fixed names marked translate="no", like a spreadsheet's "collections"
    // column
    private static string VisibleText(string html)
    {
        var withoutCode = HiddenMarkup().Replace(html, " ");
        var labels = ShownAttribute().Matches(withoutCode).Select(match => match.Groups["value"].Value);
        return WebUtility.HtmlDecode(string.Join(" ", [Tag().Replace(withoutCode, " "), .. labels]));
    }

    [GeneratedRegex(@"\b(series|artworks?|collections?|works?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RenamedWord();

    [GeneratedRegex(@"<(script|style)\b.*?</\1>|<!--.*?-->|<(?<tag>\w+)[^>]*\btranslate=""no""[^>]*>.*?</\k<tag>>", RegexOptions.Singleline)]
    private static partial Regex HiddenMarkup();

    [GeneratedRegex(@"\b(aria-label|title|alt|placeholder)=""(?<value>[^""]*)""")]
    private static partial Regex ShownAttribute();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();
}
