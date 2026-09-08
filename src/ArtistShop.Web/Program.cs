using ArtistShop.Web.Components;
using ArtistShop.Web.Database;
using ArtistShop.Web.Identity;
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

// identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(identityConnectionString)
);

builder
    .Services.AddDefaultIdentity<ApplicationUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

var app = builder.Build();

var databaseInitializer = app.Services.GetRequiredService<DatabaseInitializer>();
await databaseInitializer.EnsureDatabaseExistsAsync(shopConnectionString);
await databaseInitializer.EnsureDatabaseExistsAsync(identityConnectionString);

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

// identity
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
