using System.Text.RegularExpressions;

namespace ArtistShop.Web.Tests.App;

[Collection(TestAppCollection.Name)]
public sealed partial class ContentSecurityPolicyTests(TestApp app)
{
    // the import map is the page's one inline script, allowed by the header's nonce, which changes
    // with every request
    [Fact]
    public async Task APageAllowsOnlyItsOwnScriptsAndItsImportMap()
    {
        var client = app.ClientFor(TestApp.FirstSiteHost);

        var first = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var second = await client.GetAsync("/", TestContext.Current.CancellationToken);

        var policy = string.Join(";", first.Headers.GetValues("Content-Security-Policy"));
        var nonce = Assert.Single(NonceSource().Matches(policy)).Groups[1].Value;
        Assert.Contains("script-src 'self' 'nonce-", policy);
        Assert.Contains("frame-ancestors 'self'", policy);
        Assert.Contains(
            $"nonce=\"{nonce}\"",
            await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        Assert.DoesNotContain(nonce, string.Join(";", second.Headers.GetValues("Content-Security-Policy")));
    }

    [GeneratedRegex("'nonce-([^']+)'")]
    private static partial Regex NonceSource();
}
