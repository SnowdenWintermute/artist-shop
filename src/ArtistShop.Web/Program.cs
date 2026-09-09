using ArtistShop.Web.Components;
using ArtistShop.Web.Components.Account;
using ArtistShop.Web.Database;
using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var shopConnectionString =
    builder.Configuration.GetConnectionString("ArtistShop")
    ?? throw new InvalidOperationException("ConnectionStrings:ArtistShop is not set.");

var identityConnectionString =
    builder.Configuration.GetConnectionString("ArtistShopIdentity")
    ?? throw new InvalidOperationException("ConnectionStrings:ArtistShopIdentity is not set.");

// domain database
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<SchemaMigrator>();
builder.Services.AddSingleton(new SqlConnectionFactory(shopConnectionString));

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

var schemaMigrator = app.Services.GetRequiredService<SchemaMigrator>();
schemaMigrator.Upgrade(shopConnectionString);

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);
}

Console.WriteLine("Database initialization completed.");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// identity
app.MapAdditionalIdentityEndpoints();

app.Run();
