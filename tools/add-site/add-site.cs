// Adds a site to the dev platform database: its hosts, its own database and its image folders. Run
// from the repo root after `. ./env.sh`, with the app started at least once on the current code,
// since that is what makes the platform database:
//
//   dotnet run tools/add-site/add-site.cs -- --host site2.localhost
//   dotnet run tools/add-site/add-site.cs -- --host site2.localhost --host other.localhost
//
// The first host is the site's main one. Restart the app afterwards: it reads the hosts at startup.

#:project ../../src/ArtistShop.Web/ArtistShop.Web.csproj
// a file-based program is built for native AOT unless told otherwise, and Dapper, which the
// repository uses, generates code as it runs
#:property PublishAot=false

using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Images;
using ArtistShop.Web.Sites;
using Npgsql;

// src/ArtistShop.Web/appsettings.json's ImageStorage:RootPath, which is relative to that folder
const string ImageStorageRoot = "content/images";

var hosts = new List<HostName>();

for (var index = 0; index < args.Length; index += 1)
{
    if (args[index] is "--host" && index + 1 < args.Length && HostName.Read(args[index + 1]) is { } host)
    {
        hosts.Add(host);
        index += 1;
        continue;
    }

    Console.Error.WriteLine($"Not a --host and a host name: {args[index]}");
    return 1;
}

if (hosts.Count is 0)
{
    Console.Error.WriteLine("Give the new site at least one --host.");
    return 1;
}

var platformConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ArtistShopPlatform");

if (string.IsNullOrEmpty(platformConnectionString))
{
    Console.Error.WriteLine("ConnectionStrings__ArtistShopPlatform isn't set. Run `. ./env.sh` first.");
    return 1;
}

await using var platformDataSource = NpgsqlDataSource.Create(platformConnectionString);
await using var siteDatabases = new SiteDatabases(
    platformConnectionString,
    SiteDatabases.DatabaseNamePrefix,
    new SiteDatabaseSettings { MaximumPoolSize = 1, ConnectionIdleLifetime = TimeSpan.FromSeconds(10) }
);

var provisioner = new SiteProvisioner(
    new SiteRepository(platformDataSource),
    siteDatabases,
    new ImageStorageSettings(Path.GetFullPath(ImageStorageRoot))
);

try
{
    var siteId = await provisioner.CreateAsync(hosts);
    Console.WriteLine($"Added site {siteId.Value} at {string.Join(", ", hosts.Select(host => host.Value))}. Restart the app to reach it.");
    return 0;
}
catch (NameAlreadyInUseException)
{
    Console.Error.WriteLine("Another site already has one of those hosts.");
    return 1;
}
