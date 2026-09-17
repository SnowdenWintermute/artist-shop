using ArtistShop.Web.Components;
using ArtistShop.Web.Components.Account;
using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Identity;
using ArtistShop.Web.Images;
using ArtistShop.Web.Utilities;
using BlazorBlueprint.Primitives.Extensions;
using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

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

var shopConnectionString =
    builder.Configuration.GetConnectionString("ArtistShop")
    ?? throw new InvalidOperationException("ConnectionStrings:ArtistShop is not set.");

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

builder.Services.AddSingleton(new ImageStorage(imageStorageRootPath));

// refuse libvips file readers that aren't built to handle hostile files
// full name needed: "NetVips" alone means the namespace, and the setting lives on the NetVips class inside it
NetVips.NetVips.BlockUntrusted = true;

// one thread per image; ImageProcessingLimiter decides how many images run at once
NetVips.NetVips.Concurrency = 1;

// libvips caches recent operations to reuse them, but each upload is processed once, so the cache
// would only hold memory the limiter's estimates don't count
NetVips.Cache.Max = 0;

var imageProcessingSettings = ValidatedSettings.Read<ImageProcessingSettings>(builder.Configuration, "ImageProcessing");
var imageProcessingCapacity = ImageProcessingCapacity.FromHost(imageProcessingSettings);
builder.Services.AddSingleton(imageProcessingSettings);
builder.Services.AddSingleton(
    new ImageProcessingLimiter(imageProcessingCapacity, imageProcessingSettings.BusyRetryAfter)
);

builder.Services.AddSingleton<ImageProcessor>();
builder.Services.AddSingleton<ImageUploadStore>();

// reads from appsettings.json, environment variables or any other configuration source
var orphanedImageSweepSettings = ValidatedSettings.Read<OrphanedImageSweepSettings>(
    builder.Configuration,
    "OrphanedImageSweep"
);
builder.Services.AddSingleton(orphanedImageSweepSettings);
builder.Services.AddScoped<OrphanedImageSweeper>();

// hosted service
builder.Services.AddHostedService<OrphanedImageSweepService>();

// domain database
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<SchemaMigrator>();
builder.Services.AddSingleton(new SqlConnectionFactory(shopConnectionString));
SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
builder.Services.AddScoped<ArtworkRepository>();
builder.Services.AddScoped<SeriesRepository>();
builder.Services.AddScoped<ArtworkImageRepository>();
builder.Services.AddScoped<ArtworkFieldRepository>();
builder.Services.AddScoped<ArtworkTypeRepository>();
builder.Services.AddScoped<ProductTypeRepository>();
builder.Services.AddScoped<VocabularyRepository>();
builder.Services.AddScoped<VocabularyTermRepository>();

// identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(identityConnectionString)
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

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

/////////////////////////////
var app = builder.Build();

/////////////////////////////

var databaseInitializer = app.Services.GetRequiredService<DatabaseInitializer>();
await databaseInitializer.EnsureDatabaseExistsAsync(shopConnectionString);
await databaseInitializer.EnsureDatabaseExistsAsync(identityConnectionString);
// not the identity database: `dotnet ef database update` can create that one with the server's
// default, and identity compares its uppercased Normalized columns, so collation doesn't matter there
await databaseInitializer.VerifyCollationAsync(shopConnectionString);

var schemaMigrator = app.Services.GetRequiredService<SchemaMigrator>();
schemaMigrator.Upgrade(shopConnectionString);

var imageStorage = app.Services.GetRequiredService<ImageStorage>();
Directory.CreateDirectory(imageStorage.Originals);
Directory.CreateDirectory(imageStorage.Variants);

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);
}

Console.WriteLine("Database initialization completed.");
Console.WriteLine($"Image processing capacity: {imageProcessingCapacity}");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseStaticFiles(
    new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(imageStorage.Variants),
        RequestPath = ImageUrls.VariantsRequestPath,
    }
);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// identity
app.MapAdditionalIdentityEndpoints();

// image endpoints
app.MapImageUploadEndpoints();

app.Run();
