namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Images;
using DbUp;

// Makes new sites, and gets any site ready to serve: its own database made and brought up to date,
// and its image folders
public class SiteProvisioner(
    SiteRepository siteRepository,
    SiteDatabases siteDatabases,
    ImageStorageSettings imageStorageSettings
)
{
    // the first host is the site's main one. ownerUserId is Identity's id for the owner's account
    public async Task<SiteId> CreateAsync(IReadOnlyList<HostName> hosts, string ownerUserId)
    {
        var siteId = await siteRepository.AddAsync(hosts, ownerUserId);
        Prepare(siteId);
        return siteId;
    }

    // Safe to run again, and run at every startup for every site: a new migration reaches every
    // site, and a site whose creation stopped after its row was added is finished
    public void Prepare(SiteId siteId)
    {
        var connectionString = siteDatabases.ConnectionString(siteId);

        EnsureDatabase.For.PostgresqlDatabase(connectionString);
        SchemaMigrator.Site.Upgrade(connectionString);
        ImageStorage.ForSite(imageStorageSettings, siteId).CreateFolders();
    }
}
