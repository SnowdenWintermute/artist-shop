using System.Net.Http.Headers;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Platform;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Identity;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using System.Text.RegularExpressions;
using ArtistShop.Web.Email;
using ArtistShop.Web.Images;
using ArtistShop.Web.Tests.Images;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

// One app for the whole run, shared by every test class that asks for it. The classes run in parallel,
// so a test keeps to its own accounts and sites, or to the first site without changing it. One that
// acts on the whole platform goes in RunAloneCollection
[assembly: AssemblyFixture(typeof(ArtistShop.Web.Tests.App.TestApp))]

namespace ArtistShop.Web.Tests.App;

// The whole app, Program.cs and all, answering requests in the test process with no network port
// (ASP.NET's WebApplicationFactory). Its own databases on development's server, dropped as the
// fixture starts, and its own image folder. Program.cs's startup runs as the fixture starts:
// migrations and the operator's account. Then the fixture adds one site, as sign-up would
public sealed partial class TestApp : WebApplicationFactory<Program>, IAsyncLifetime
{
    // dropped at the start of each run rather than at the end, so a failed run's rows can still be
    // inspected. Its site schemas are inside its platform database, so they go with it
    private const string PlatformDatabaseName = "artist_shop_tests_app_platform";
    private const string IdentityDatabaseName = "artist_shop_tests_app_identity";

    public const string PlatformHost = "platform.test";
    public const string FirstSiteHost = "site1.test";
    public const string FirstSiteOwnerEmail = "owner@example.com";

    public const string OperatorEmail = "operator@example.com";
    // meets Identity's default password rules
    public const string Password = "Test-password-1";

    public ImmediateImageSizeQueue ImageSizeQueue { get; } = new();

    // every email the app sends, kept rather than sent
    public CapturingMailer Mailer { get; } = new();

    // the tests each use addresses of their own, so refusing by address keeps them apart
    public RefusingEmailSendLimit EmailSendLimit { get; } = new();

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
            TestDatabaseFixture.ConnectionStringFor(PlatformDatabaseName)
        );
        builder.UseSetting(
            "ConnectionStrings:ArtistShopIdentity",
            TestDatabaseFixture.ConnectionStringFor(IdentityDatabaseName)
        );
        builder.UseSetting("ImageStorage:RootPath", _imageStorageRootPath);

        builder.UseSetting("Platform:Host", PlatformHost);
        builder.UseSetting("Platform:OperatorEmail", OperatorEmail);
        builder.UseSetting("Platform:OperatorPassword", Password);
        // never used to reach Google: no test follows the redirect there
        builder.UseSetting("Authentication:Google:ClientId", "test-client-id");
        builder.UseSetting("Authentication:Google:ClientSecret", "test-client-secret");

        // the migrations log every script, which buries the test results
        builder.UseSetting("Logging:LogLevel:Default", "Warning");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<Mailer>(Mailer);
            services.AddSingleton<EmailQueue>(new ImmediateEmailQueue(Mailer));
            services.AddSingleton<EmailSendLimit>(EmailSendLimit);
            services.AddSingleton<ImageSizeQueue>(ImageSizeQueue);
            // nothing reaches the background queue, and its startup look for missing sizes would race
            // tests that leave sizes out on purpose. ImageSizeQueueWorkerTests start one of their own
            services.Remove(services.Single(service => service.ImplementationType == typeof(ImageSizeQueueWorker)));
            services.AddTransient<IStartupFilter, FakeGoogle>();

            // every request runs the security stamp check, which production runs on a cookie's first
            // request each half hour; that request's user is rebuilt from the account, and must still
            // carry its session id
            services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        });
    }

    public async ValueTask InitializeAsync()
    {
        // before Services, which starts the app and its migrations
        await TestDatabaseFixture.DropDatabasesAsync(PlatformDatabaseName, IdentityDatabaseName);

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

    // An account with TestApp.Password whose email was never confirmed, as registering made them before
    // accounts waited for their email; its email
    public async Task<string> MakeUnconfirmedAccountAsync()
    {
        var email = $"{Guid.NewGuid():n}@example.com";
        using var scope = Services.CreateScope();
        SeededAccounts.ThrowIfFailed(
            await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                .CreateAsync(new ApplicationUser { UserName = email, Email = email }, Password),
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
        await AddAdminAsync(siteId, email);
        return email;
    }

    // the account with this email invited to the site and accepted
    public async Task AddAdminAsync(SiteId siteId, string email)
    {
        var address = EmailAddress.Read(email) ?? throw new InvalidOperationException("Not an email address.");
        var invites = Services.GetRequiredService<SiteInviteRepository>();

        await invites.AddAsync(siteId, address, DateTimeOffset.UtcNow.AddDays(1));
        if (!await invites.AcceptAsync(siteId, address, await UserIdAsync(email)))
        {
            throw new InvalidOperationException("The invitation wasn't accepted.");
        }
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

    // a client on the host, signed in there with the account's email and TestApp.Password. On a
    // website that's as a browser does it: signed in on the platform, then handed back
    public async Task<HttpClient> SignedInClientAsync(string host, string email)
    {
        var client = await SignedInOnPlatformClientAsync(host, email);

        if (host != PlatformHost)
        {
            var landed = await FollowSiteSignInAsync(client, "/Account/Login");
            if (landed.StatusCode is not System.Net.HttpStatusCode.Redirect)
            {
                throw new InvalidOperationException($"The handoff to {host} didn't sign {email} in: {landed.StatusCode}.");
            }
        }

        return client;
    }

    // a client on the host, signed in on the platform only: on a website, not yet handed back there
    public async Task<HttpClient> SignedInOnPlatformClientAsync(string host, string email)
    {
        var client = ClientFor(host);
        var response = await PostFormAsync(
            client,
            $"http://{PlatformHost}/Account/Login",
            "login",
            new() { ["Input.Email"] = email, ["Input.Password"] = Password }
        );

        if (response.StatusCode is not System.Net.HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException($"Signing in as {email} didn't redirect: {response.StatusCode}.");
        }

        return client;
    }

    // From a website's page that sends the client to sign in, through the platform and back: the
    // handoff's answer, a redirect to where it started once signed in. The client must be signed in
    // on the platform, or the platform's answer is its own Login
    public static async Task<HttpResponseMessage> FollowSiteSignInAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        // the website's Login, then the platform's SignInTo, then the website's Handoff
        for (var step = 0; step < 2; step++)
        {
            response = await client.GetAsync(
                response.Headers.Location ?? throw new InvalidOperationException($"{path} stopped at {response.StatusCode}."),
                TestContext.Current.CancellationToken
            );
        }

        return response;
    }

    // The sign-in cookie of a new sign-in on the host, as "name=value", for a test that replays a copy
    // of it after that sign-in has ended. client is signed in with that same cookie
    public async Task<(HttpClient Client, string Cookie)> SignInWithCookieAsync(string host, string email)
    {
        var client = ClientFor(host);
        var response = await PostFormAsync(
            client,
            "/Account/Login",
            "login",
            new() { ["Input.Email"] = email, ["Input.Password"] = Password }
        );
        var cookie = response
            .Headers.GetValues("Set-Cookie")
            .Single(header => header.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal))
            .Split(';')[0];

        return (client, cookie);
    }

    // a client on the host that sends only this cookie, and keeps none it's given
    public HttpClient ClientWithCookie(string host, string cookie)
    {
        var client = CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri($"http://{host}"),
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    // the layout's Logout button on the client's own host
    public static Task<HttpResponseMessage> SignOutAsync(HttpClient client) =>
        SignOutAsync(client, client.BaseAddress?.Host ?? throw new InvalidOperationException("The client has no host."));

    // the layout's Logout button on the host, from its home page, which every host has
    public static async Task<HttpResponseMessage> SignOutAsync(HttpClient client, string host)
    {
        var page = await client.GetStringAsync($"http://{host}/", TestContext.Current.CancellationToken);

        return await client.PostAsync(
            $"http://{host}/Account/Logout",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["ReturnUrl"] = "",
                    ["__RequestVerificationToken"] = AntiforgeryToken().Match(page).Groups["token"].Value,
                }
            ),
            TestContext.Current.CancellationToken
        );
    }

    // Signing in on the platform with Google, as though back from Google with this login: the
    // callback's answer, a redirect to /sites once signed in
    public static async Task<HttpResponseMessage> SignInWithGoogleAsync(HttpClient client, FakeGoogleLogin login)
    {
        var fakeGoogle = QueryHelpers.AddQueryString(
            $"http://{PlatformHost}{FakeGoogle.Path}",
            new Dictionary<string, string?>
            {
                ["email"] = login.Email,
                ["googleId"] = login.GoogleId,
                ["emailVerified"] = login.EmailVerified ? "true" : "false",
            }
        );
        (await client.GetAsync(fakeGoogle, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        return await client.GetAsync(
            $"http://{PlatformHost}/Account/ExternalLogin?Action=LoginCallback&ReturnUrl=%2Fsites",
            TestContext.Current.CancellationToken
        );
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
        var token = await AntiforgeryTokenAsync(client, path);

        return await client.PostAsync(
            path,
            new FormUrlEncodedContent(
                new Dictionary<string, string>(fields) { ["_handler"] = formName, ["__RequestVerificationToken"] = token }
            ),
            TestContext.Current.CancellationToken
        );
    }

    // An upload page's script sending one file, as a browser does: the page loaded for its
    // antiforgery token, then the file as the form field "file" with the other fields beside it
    public static async Task<HttpResponseMessage> PostFileAsync(
        HttpClient client,
        string pagePath,
        string uploadPath,
        string fileName,
        string contentType,
        byte[] bytes,
        Dictionary<string, string> fields
    )
    {
        var token = await AntiforgeryTokenAsync(client, pagePath);
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var content = new MultipartFormDataContent
        {
            { file, "file", fileName },
            { new StringContent(token), "__RequestVerificationToken" },
        };

        foreach (var (name, value) in fields)
        {
            content.Add(new StringContent(value), name);
        }

        return await client.PostAsync(uploadPath, content, TestContext.Current.CancellationToken);
    }

    // the token a form on the page carries, for a request posted as that form would be
    public static async Task<string> AntiforgeryTokenAsync(HttpClient client, string pagePath)
    {
        var page = await client.GetStringAsync(pagePath, TestContext.Current.CancellationToken);
        return AntiforgeryToken().Match(page).Groups["token"].Value;
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

// For the tests that act on the whole platform, such as signing everyone out, which would undo the
// other tests' sign-ins. xUnit runs this collection after the others, with nothing beside it
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RunAloneCollection
{
    public const string Name = "Run alone";
}
