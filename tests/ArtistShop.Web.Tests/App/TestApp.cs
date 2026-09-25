using ArtistShop.Web.Tests.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArtistShop.Web.Tests.App;

// The whole app, Program.cs and all, answering requests in the test process with no network port
// (ASP.NET's WebApplicationFactory). Its own databases on development's server, which
// TestDatabaseFixture drops at the start of each run, and its own image folder. Starts on the
// first request or the first use of Services, running Program.cs's startup: migrations, the
// operator's account and the first site
public sealed class TestApp : WebApplicationFactory<Program>
{
    public const string PlatformHost = "platform.test";
    public const string FirstSiteHost = "site1.test";

    public const string OperatorEmail = "operator@example.com";
    // meets Identity's default password rules
    public const string Password = "Test-password-1";

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
        builder.UseSetting("SiteDatabaseNamePrefix", TestDatabaseFixture.SiteDatabaseNamePrefix + "app_");
        builder.UseSetting("ImageStorage:RootPath", _imageStorageRootPath);

        builder.UseSetting("Platform:Host", PlatformHost);
        builder.UseSetting("Platform:OperatorEmail", OperatorEmail);
        builder.UseSetting("Platform:OperatorPassword", Password);

        builder.UseSetting("FirstSite:Hosts:0", FirstSiteHost);
        builder.UseSetting("FirstSite:OwnerEmail", "owner@example.com");
        builder.UseSetting("FirstSite:OwnerPassword", Password);

        // the migrations log every script, which buries the test results
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
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

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (Directory.Exists(_imageStorageRootPath))
        {
            Directory.Delete(_imageStorageRootPath, recursive: true);
        }
    }
}

// Every test class that runs the app shares one, since they'd otherwise each migrate the same
// databases at once
[CollectionDefinition(Name)]
public sealed class TestAppCollection : ICollectionFixture<TestApp>
{
    public const string Name = "TestApp";
}
