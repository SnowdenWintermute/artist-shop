using ArtistShop.Web.Components;
using ArtistShop.Web.Components.Account;
using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Email;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Identity;
using ArtistShop.Web.Images;
using ArtistShop.Web.Publishing;
using ArtistShop.Web.Search;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Utilities;
using BlazorBlueprint.Primitives.Extensions;
using Dapper;
using DbUp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);

// Add services to the container.
builder
    .Services.AddRazorComponents()
    // detailed errors send the exception and its stack trace to the browser, so they stay in development
    .AddInteractiveServerComponents(options =>
        options.DetailedErrors = builder.Environment.IsDevelopment()
    );
builder.Services.AddBlazorBlueprintPrimitives();

// the platform database, which lists the sites and holds each site's own schema (SiteDatabases)
var platformConnectionString =
    builder.Configuration.GetConnectionString("ArtistShopPlatform")
    ?? throw new InvalidOperationException("ConnectionStrings:ArtistShopPlatform is not set.");

var identityConnectionString =
    builder.Configuration.GetConnectionString("ArtistShopIdentity")
    ?? throw new InvalidOperationException("ConnectionStrings:ArtistShopIdentity is not set.");

var imageStorageRootPath = Path.GetFullPath(
    Path.Combine(
        builder.Environment.ContentRootPath,
        builder.Configuration["ImageStorage:RootPath"]
            ?? throw new InvalidOperationException("ImageStorage:RootPath is not set.")
    )
);

var imageStorageSettings = new ImageStorageSettings(imageStorageRootPath);
builder.Services.AddSingleton(imageStorageSettings);

// sites: the platform's tables, each site's own schema, and which one a request is for
const string PlatformDataSourceKey = "platform";
// a factory rather than an instance, so the container disposes the data source when the app stops
builder.Services.AddKeyedSingleton(PlatformDataSourceKey, (_, _) => NpgsqlDataSource.Create(platformConnectionString));
builder.Services.AddSingleton(services =>
    new SiteRepository(services.GetRequiredKeyedService<NpgsqlDataSource>(PlatformDataSourceKey))
);
builder.Services.AddSingleton(services =>
    new SignUpCodeRepository(services.GetRequiredKeyedService<NpgsqlDataSource>(PlatformDataSourceKey))
);
builder.Services.AddSingleton(services =>
    new SiteInviteRepository(services.GetRequiredKeyedService<NpgsqlDataSource>(PlatformDataSourceKey))
);

// every site's schema, in the platform database and reached through one shared pool
const string SitesDataSourceKey = "sites";
var siteDatabaseSettings = ValidatedSettings.Read<SiteDatabaseSettings>(builder.Configuration, "SiteDatabases");
builder.Services.AddKeyedSingleton(
    SitesDataSourceKey,
    (_, _) => SiteDataSource.Create(platformConnectionString, siteDatabaseSettings)
);
builder.Services.AddSingleton(services =>
    new SiteDatabases(services.GetRequiredKeyedService<NpgsqlDataSource>(SitesDataSourceKey))
);
builder.Services.AddSingleton(new SiteSchemas(platformConnectionString));

// the platform's own host, where artists sign up; every other host is a site's
var platformHost =
    HostName.Read(builder.Configuration["Platform:Host"] ?? "")
    ?? throw new InvalidOperationException("Platform:Host isn't a host name.");
var platformName = builder.Configuration["Platform:Name"] is { Length: > 0 } name
    ? name
    : throw new InvalidOperationException("Platform:Name isn't set.");
builder.Services.AddSingleton(new PlatformSettings(platformHost, platformName));
builder.Services.AddSingleton(services =>
    new HostDirectory(
        services.GetRequiredService<SiteRepository>().GetHostsAsync,
        services.GetRequiredService<PlatformSettings>()
    )
);
builder.Services.AddSingleton<SiteProvisioner>();
builder.Services.AddScoped<SiteSignUp>();
builder.Services.AddScoped<SiteMemberAccounts>();
builder.Services.AddScoped<SiteDeletions>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(CurrentHost.From);
builder.Services.AddScoped(CurrentSite.From);
builder.Services.AddScoped<CircuitHandler, HostCircuitStart>();

builder.Services.AddScoped(services =>
    ImageStorage.ForSite(imageStorageSettings, services.GetRequiredService<CurrentSite>().Id)
);

// refuse libvips file readers that aren't built to handle hostile files
// full name needed: "NetVips" alone means the namespace, and the setting lives on the NetVips class inside it
NetVips.NetVips.BlockUntrusted = true;

// one thread per image; ImageProcessingLimiter decides how many images run at once
NetVips.NetVips.Concurrency = 1;

// libvips caches recent operations to reuse them, but each upload is processed once, so the cache
// would only hold memory the limiter's estimates don't count
NetVips.Cache.Max = 0;

var imageProcessingSettings = ValidatedSettings.Read<ImageProcessingSettings>(
    builder.Configuration,
    "ImageProcessing"
);
var imageProcessingCapacity = ImageProcessingCapacity.FromHost(imageProcessingSettings);
builder.Services.AddSingleton(imageProcessingSettings);
builder.Services.AddSingleton(
    new ImageProcessingLimiter(imageProcessingCapacity, imageProcessingSettings.BusyRetryAfter)
);

// scoped, since they work in the current site's folder; the limiter above is shared by every site
builder.Services.AddScoped<ImageProcessor>();
builder.Services.AddScoped<ImageUploadStore>();
builder.Services.AddImageUploadRateLimiter();

// reads from appsettings.json, environment variables or any other configuration source
var orphanedImageSweepSettings = ValidatedSettings.Read<OrphanedImageSweepSettings>(
    builder.Configuration,
    "OrphanedImageSweep"
);
builder.Services.AddSingleton(orphanedImageSweepSettings);
builder.Services.AddSingleton<OrphanedImageSweeper>();

// hosted service
builder.Services.AddHostedService<OrphanedImageSweepService>();

// deleted sites, once their grace period is over
builder.Services.AddSingleton<SiteEraser>();
builder.Services.AddHostedService<SiteEraseService>();

// a site's own schema
SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

// the database's artwork_type_id fills the ArtworkTypeId property
DefaultTypeMap.MatchNamesWithUnderscores = true;

// each built on the current site's schema, so everything they read and write is that site's
void AddSiteRepository<T>(Func<SiteDatabase, T> create)
    where T : class =>
    builder.Services.AddScoped(services =>
        create(services.GetRequiredService<SiteDatabases>().For(services.GetRequiredService<CurrentSite>().Id))
    );

AddSiteRepository(database => new ArtworkRepository(database));
AddSiteRepository(database => new SeriesRepository(database));
AddSiteRepository(database => new ArtworkImageRepository(database));
AddSiteRepository(database => new ArtworkFieldRepository(database));
AddSiteRepository(database => new ArtworkTypeRepository(database));
AddSiteRepository(database => new ProductTypeRepository(database));
AddSiteRepository(database => new VocabularyRepository(database));
AddSiteRepository(database => new VocabularyTermRepository(database));
AddSiteRepository(database => new PostRepository(database));
AddSiteRepository<ArtworkSearch>(database => new SqlArtworkTitleSearch(database));

// identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(identityConnectionString)
);

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<
    AuthenticationStateProvider,
    IdentityRevalidatingAuthenticationStateProvider
>();

builder
    .Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder
    .Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.User.RequireUniqueEmail = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// email: to Mailpit in dev, to the provider production's settings name
var emailSettings = ValidatedSettings.Read<EmailSettings>(builder.Configuration, "Email");
builder.Services.AddSingleton(emailSettings);
builder.Services.AddSingleton<Mailer, SmtpMailer>();
builder.Services.AddSingleton<AccountEmails>();
builder.Services.AddSingleton<IEmailSender<ApplicationUser>>(services => services.GetRequiredService<AccountEmails>());
builder.Services.AddScoped<AccountRegistration>();
builder.Services.AddScoped<AccountDeletion>();
builder.Services.AddSingleton<SiteEmails>();

// site rights come from site membership in the platform database, not Identity's roles, which are
// the same on every site. Scoped, since the handler asks about the current site
builder.Services.AddScoped<IAuthorizationHandler, SiteRoleHandler>();
builder
    .Services.AddAuthorizationBuilder()
    .AddPolicy(
        SitePolicies.Admin,
        policy =>
            policy.RequireAuthenticatedUser().AddRequirements(new SiteRoleRequirement([SiteRole.Owner, SiteRole.Admin]))
    )
    .AddPolicy(
        SitePolicies.Owner,
        policy => policy.RequireAuthenticatedUser().AddRequirements(new SiteRoleRequirement([SiteRole.Owner]))
    )
    .AddPolicy(PlatformPolicies.Operator, policy => policy.RequireRole(PlatformOperator.RoleName));

/////////////////////////////
var app = builder.Build();

/////////////////////////////

EnsureDatabase.For.PostgresqlDatabase(platformConnectionString);
SchemaMigrator.Platform.Upgrade(platformConnectionString, "public");

using (var scope = app.Services.CreateScope())
{
    // creates the identity database too, if it isn't there yet
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();

    await PlatformOperator.SyncAsync(scope.ServiceProvider);
}

var siteRepository = app.Services.GetRequiredService<SiteRepository>();
var siteProvisioner = app.Services.GetRequiredService<SiteProvisioner>();

// every site's schema brought up to date, and its folders made
foreach (var siteId in await siteRepository.GetIdsAsync())
{
    siteProvisioner.Prepare(siteId);
}

await app.Services.GetRequiredService<HostDirectory>().ReloadAsync();

Console.WriteLine("Database initialization completed.");
Console.WriteLine($"Image processing capacity: {imageProcessingCapacity}");

// Configure the HTTP request pipeline.

// first, so a host that's neither the platform's nor a site's is turned away before anything else runs
app.UseKnownHosts();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseServedOnHosts();

// called here rather than left for ASP.NET to add, which it does at the start of the pipeline:
// SitePolicies.Admin asks about the current site, so it must only run once the page is known to be
// a site's
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.UseRateLimiter();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// identity
app.MapAdditionalIdentityEndpoints();

// image endpoints
app.MapImageUploadEndpoints();
app.MapVariantEndpoints();

// post editor endpoints
app.MapVideoLinkEndpoints();

app.Run();
