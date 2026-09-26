using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Platform;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Identity;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using System.Text.RegularExpressions;
using ArtistShop.Web.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// The whole app, Program.cs and all, answering requests in the test process with no network port
// (ASP.NET's WebApplicationFactory). Its own databases on development's server, which
// TestDatabaseFixture drops at the start of each run, and its own image folder. Program.cs's
// startup runs as the fixture starts: migrations and the operator's account. Then the fixture adds
// one site, as sign-up would
public sealed partial class TestApp : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string PlatformHost = "platform.test";
    public const string FirstSiteHost = "site1.test";
    public const string FirstSiteOwnerEmail = "owner@example.com";

    public const string OperatorEmail = "operator@example.com";
    // meets Identity's default password rules
    public const string Password = "Test-password-1";

    // every email the app sends, kept rather than sent
    public CapturingMailer Mailer { get; } = new();

    private SiteId? _firstSiteId;

    public SiteId FirstSiteId => _firstSiteId ?? throw new InvalidOperationException("The first site isn't made yet.");

    private readonly string _imageStorageRootPath = Path.Combine(
        Path.GetTempPath(),
        "artist-shop-app-tests",
        Guid.NewGuid().ToString("n")
    );

    // UseSetting rather than the environment variables env.sh sets, which these take precedence over
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:ArtistShopPlatform",
            TestDatabaseFixture.ConnectionStringFor(TestDatabaseFixture.AppPlatformDatabaseName)
        );
        builder.UseSetting(
            "ConnectionStrings:ArtistShopIdentity",
            TestDatabaseFixture.ConnectionStringFor(TestDatabaseFixture.AppIdentityDatabaseName)
        );
        builder.UseSetting("ImageStorage:RootPath", _imageStorageRootPath);

        builder.UseSetting("Platform:Host", PlatformHost);
        builder.UseSetting("Platform:OperatorEmail", OperatorEmail);
        builder.UseSetting("Platform:OperatorPassword", Password);

        // the migrations log every script, which buries the test results
        builder.UseSetting("Logging:LogLevel:Default", "Warning");

        builder.ConfigureTestServices(services => services.AddSingleton<Mailer>(Mailer));
    }

    public async ValueTask InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var owner = new ApplicationUser
        {
            UserName = FirstSiteOwnerEmail,
            Email = FirstSiteOwnerEmail,
            EmailConfirmed = true,
        };
        SeededAccounts.ThrowIfFailed(await userManager.CreateAsync(owner, Password), "Creating the first site's owner");

        var host = HostName.Read(FirstSiteHost) ?? throw new InvalidOperationException("Not a host.");
        _firstSiteId = await Services.GetRequiredService<SiteProvisioner>().CreateAsync([host], owner.Id);
        await Services.GetRequiredService<HostDirectory>().ReloadAsync();
    }

    // a confirmed account of its own, with TestApp.Password; its email
    public async Task<string> MakeAccountAsync()
    {
        var email = $"{Guid.NewGuid():n}@example.com";
        using var scope = Services.CreateScope();
        SeededAccounts.ThrowIfFailed(
            await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                .CreateAsync(new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true }, Password),
            "Making the account"
        );
        return email;
    }

    // Identity's id for the account with this email
    public async Task<string> UserIdAsync(string email)
    {
        using var scope = Services.CreateScope();
        var account =
            await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"{email} has no account.");
        return account.Id;
    }

    // a site of its own, owned by a new account, for a test that changes who owns it
    public async Task<TestSite> MakeSiteAsync()
    {
        var ownerEmail = await MakeAccountAsync();
        var host = $"{Guid.NewGuid():n}.test";
        var siteId = await Services
            .GetRequiredService<SiteProvisioner>()
            .CreateAsync([HostName.Read(host) ?? throw new InvalidOperationException("Not a host.")], await UserIdAsync(ownerEmail));
        await Services.GetRequiredService<HostDirectory>().ReloadAsync();

        return new TestSite(siteId, host, ownerEmail);
    }

    public Task<string> MakeFirstSiteAdminAsync() => MakeAdminAsync(FirstSiteId);

    // a new account, invited to the site and accepted, as My websites accepts; its email
    public async Task<string> MakeAdminAsync(SiteId siteId)
    {
        var email = await MakeAccountAsync();
        var address = EmailAddress.Read(email) ?? throw new InvalidOperationException("Not an email address.");
        var invites = Services.GetRequiredService<SiteInviteRepository>();

        await invites.AddAsync(siteId, address, DateTimeOffset.UtcNow.AddDays(1));
        if (!await invites.AcceptAsync(siteId, address, await UserIdAsync(email)))
        {
            throw new InvalidOperationException("The invitation wasn't accepted.");
        }

        return email;
    }

    // a new code, as the operator page makes one
    public async Task<SignUpCode> MakeSignUpCodeAsync()
    {
        var code = SignUpCode.New();
        await Services
            .GetRequiredService<SignUpCodeRepository>()
            .AddAsync(code, "a test", DateTimeOffset.UtcNow.AddDays(1));
        return code;
    }

    // a client whose requests are for the given host, and that shows redirects rather than following them
    public HttpClient ClientFor(string host) =>
        CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri($"http://{host}"),
                AllowAutoRedirect = false,
            }
        );

    // a client on the host, signed in there with the account's email and TestApp.Password
    public async Task<HttpClient> SignedInClientAsync(string host, string email)
    {
        var client = ClientFor(host);
        var response = await PostFormAsync(
            client,
            "/Account/Login",
            "login",
            new() { ["Input.Email"] = email, ["Input.Password"] = Password }
        );

        if (response.StatusCode is not System.Net.HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException($"Signing in as {email} didn't redirect: {response.StatusCode}.");
        }

        return client;
    }

    // Loads the page for its form's antiforgery token, then posts the form, as a browser does. The
    // client keeps the antiforgery cookie between the two. formName is the EditForm's FormName, and
    // fields are named as the form names them, such as Input.Email
    public static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string path,
        string formName,
        Dictionary<string, string> fields
    )
    {
        var page = await client.GetStringAsync(path, TestContext.Current.CancellationToken);
        var token = AntiforgeryToken().Match(page).Groups["token"].Value;

        return await client.PostAsync(
            path,
            new FormUrlEncodedContent(
                new Dictionary<string, string>(fields) { ["_handler"] = formName, ["__RequestVerificationToken"] = token }
            ),
            TestContext.Current.CancellationToken
        );
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"(?<token>[^\"]+)\"")]
    private static partial Regex AntiforgeryToken();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (Directory.Exists(_imageStorageRootPath))
        {
            Directory.Delete(_imageStorageRootPath, recursive: true);
        }
    }
}

public sealed record TestSite(SiteId Id, string Host, string OwnerEmail);

// Every test class that runs the app shares one, since they'd otherwise each migrate the same
// databases at once
[CollectionDefinition(Name)]
public sealed class TestAppCollection : ICollectionFixture<TestApp>
{
    public const string Name = "TestApp";
}
