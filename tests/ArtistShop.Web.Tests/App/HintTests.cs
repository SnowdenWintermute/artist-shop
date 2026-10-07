using System.Net;
using System.Text.RegularExpressions;
using ArtistShop.Web.Components;
using ArtistShop.Web.Components.Hints;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Sites;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// Hints dismissed with their X, for the account on every website it administers, and shown again from Help.
// Signing in gives the account the profile its dismissals hang off, so these save without making one
[Collection(TestAppCollection.Name)]
public sealed partial class HintTests(TestApp app)
{
    private async Task<(TestSite Site, HttpClient Client)> SiteWithOwnerAsync()
    {
        var site = await app.MakeSiteAsync();
        return (site, await app.SignedInClientAsync(site.Host, site.OwnerEmail));
    }

    private static Task<string> PageAsync(HttpClient client, string path) =>
        client.GetStringAsync(path, TestContext.Current.CancellationToken);

    // as Hint's script sends it: with the token the page put on the hint
    private static Task<HttpResponseMessage> DismissAsync(HttpClient client, string? hint) =>
        SetAsync(client, hint, dismissed: true);

    private static async Task<HttpResponseMessage> SetAsync(HttpClient client, string? hint, bool dismissed)
    {
        var token = SaveToken().Match(await PageAsync(client, PageUrls.SeriesList));
        var fields = new Dictionary<string, string>
        {
            [token.Groups["field"].Value] = token.Groups["token"].Value,
            [DismissedHintEndpoints.DismissedField] = dismissed ? "true" : "false",
        };

        if (hint is not null)
        {
            fields[DismissedHintEndpoints.HintField] = hint;
        }

        return await client.PostAsync(
            DismissedHintEndpoints.Path,
            new FormUrlEncodedContent(fields),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task ADismissedHintNoLongerShows()
    {
        var (_, client) = await SiteWithOwnerAsync();
        Assert.Equal(["Series"], Hints(await PageAsync(client, PageUrls.SeriesList)));

        var response = await DismissAsync(client, nameof(HintType.Series));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(Hints(await PageAsync(client, PageUrls.SeriesList)));
        Assert.Equal(["StepByStepImport"], Hints(await PageAsync(client, PageUrls.Import)));
    }

    // as the Hints page's Dismiss all does
    [Fact]
    public async Task DismissingAllHidesEveryHint()
    {
        var (site, client) = await SiteWithOwnerAsync();

        await app
            .Services.GetRequiredService<DismissedHintRepository>()
            .DismissAsync(await app.UserIdAsync(site.OwnerEmail), Enum.GetValues<HintType>());

        Assert.Empty(Hints(await PageAsync(client, PageUrls.SeriesList)));
        Assert.Empty(Hints(await PageAsync(client, PageUrls.Import)));
        Assert.Empty(Hints(await PageAsync(client, PageUrls.AdminDashboard)));
    }

    [Fact]
    public async Task ADismissalHoldsOnTheAccountsOtherWebsites()
    {
        var (site, client) = await SiteWithOwnerAsync();
        var other = await app.MakeSiteAsync();
        await app.AddAdminAsync(other.Id, site.OwnerEmail);

        await DismissAsync(client, nameof(HintType.Series));

        var otherClient = await app.SignedInClientAsync(other.Host, site.OwnerEmail);
        Assert.Empty(Hints(await PageAsync(otherClient, PageUrls.SeriesList)));
    }

    [Fact]
    public async Task AnotherAccountStillSeesTheHint()
    {
        var (site, client) = await SiteWithOwnerAsync();
        var adminClient = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        await DismissAsync(client, nameof(HintType.Series));

        Assert.Equal(["Series"], Hints(await PageAsync(adminClient, PageUrls.SeriesList)));
    }

    // an import page's columns stay a click away, so its hint folds to Show help text rather than going
    [Fact]
    public async Task ADismissedImportHintFoldsToShowHelpText()
    {
        var (_, client) = await SiteWithOwnerAsync();
        Assert.False(IsFolded(await PageAsync(client, PageUrls.VocabularyImport)));

        await DismissAsync(client, nameof(HintType.VocabularyImport));

        var page = await PageAsync(client, PageUrls.VocabularyImport);
        Assert.True(IsFolded(page));
        Assert.Contains("Show help text", page);
        Assert.Equal(["VocabularyImport"], Hints(page));
    }

    // as its Show help text does
    [Fact]
    public async Task ShowingAFoldedHintAgainKeepsItShown()
    {
        var (_, client) = await SiteWithOwnerAsync();
        await DismissAsync(client, nameof(HintType.VocabularyImport));

        var response = await SetAsync(client, nameof(HintType.VocabularyImport), dismissed: false);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(IsFolded(await PageAsync(client, PageUrls.VocabularyImport)));
    }

    // the collapsing hint's own data-dismissed, not the data-dismissed-field beside it
    private static bool IsFolded(string page)
    {
        var hint = CollapsingHint().Match(page);
        Assert.True(hint.Success);
        return Regex.IsMatch(hint.Groups["attributes"].Value, @"data-dismissed(\s|$)");
    }

    // a number too, which Enum.TryParse would have taken; and none, as there's no dismissing them all here
    [Theory]
    [InlineData("Anything")]
    [InlineData("1")]
    [InlineData(null)]
    public async Task RefusesANameThatIsntAHint(string? hint)
    {
        var (_, client) = await SiteWithOwnerAsync();

        var response = await DismissAsync(client, hint);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RefusesADismissalWithoutTheAntiforgeryToken()
    {
        var (_, client) = await SiteWithOwnerAsync();

        var response = await client.PostAsync(
            DismissedHintEndpoints.Path,
            new FormUrlEncodedContent(
                new Dictionary<string, string> { [DismissedHintEndpoints.HintField] = nameof(HintType.Series) }
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // the island's first render, before its circuit connects
    [Fact]
    public async Task HelpChecksTheHintsThatShow()
    {
        var (_, client) = await SiteWithOwnerAsync();
        await DismissAsync(client, nameof(HintType.Series));

        var page = await PageAsync(client, PageUrls.Hints);

        Assert.Equal(
            [.. Enum.GetValues<HintType>().Where(type => type is not HintType.Series).Select(HintListing.Title)],
            [.. CheckedHint().Matches(page).Select(match => match.Groups["title"].Value.Trim())]
        );
        Assert.Contains(HintListing.Title(HintType.Series), page);
    }

    [Fact]
    public async Task ShowingAgainBringsTheHintBack()
    {
        var (site, client) = await SiteWithOwnerAsync();
        var dismissedHints = app.Services.GetRequiredService<DismissedHintRepository>();
        var userId = await app.UserIdAsync(site.OwnerEmail);
        await dismissedHints.DismissAsync(userId, Enum.GetValues<HintType>());

        await dismissedHints.ShowAsync(userId, [HintType.Series]);

        Assert.Equal(["Series"], Hints(await PageAsync(client, PageUrls.SeriesList)));
        Assert.Empty(Hints(await PageAsync(client, PageUrls.Import)));
    }

    private static List<string> Hints(string page) =>
        [.. HintMarker().Matches(page).Select(match => match.Groups["name"].Value)];

    [GeneratedRegex("<hint-toggle class=\"group/hint[^\"]*\"(?<attributes>[^>]*)>")]
    private static partial Regex CollapsingHint();

    [GeneratedRegex("data-hint=\"(?<name>[^\"]+)\"")]
    private static partial Regex HintMarker();

    [GeneratedRegex("data-token-field=\"(?<field>[^\"]+)\" data-token=\"(?<token>[^\"]+)\"")]
    private static partial Regex SaveToken();

    [GeneratedRegex("<input type=\"checkbox\" class=\"cursor-pointer\" checked[^>]*>(?<title>[^<]+)</label>")]
    private static partial Regex CheckedHint();
}
