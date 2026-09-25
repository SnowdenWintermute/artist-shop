namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Images;

// Makes new sites, and gets any site ready to serve: its own schema made and brought up to date,
// and its image folders
public class SiteProvisioner(
    SiteRepository siteRepository,
    SiteSchemas siteSchemas,
    ImageStorageSettings imageStorageSettings
)
{
    // the first host is the site's main one. ownerUserId is Identity's id for the owner's account
    public Task<SiteId> CreateAsync(IReadOnlyList<HostName> hosts, string ownerUserId) =>
        CreateAsync(siteId => siteRepository.AddAsync(siteId, hosts, ownerUserId));

    // The site is made ready before addSite adds its row to the platform database, so a site that's
    // listed is always ready, and if anything fails its schema is dropped and nothing, such as a
    // sign-up code, is used up
    public async Task<SiteId> CreateAsync(Func<SiteId, Task> addSite)
    {
        var siteId = await siteRepository.ReserveIdAsync();

        try
        {
            Prepare(siteId);
            await addSite(siteId);
        }
        catch
        {
            await siteSchemas.DropAsync(siteId);
            throw;
        }

        return siteId;
    }

    // Safe to run again, and run at every startup for every site, so a new migration reaches every
    // site
    public void Prepare(SiteId siteId)
    {
        siteSchemas.Migrate(siteId);
        ImageStorage.ForSite(imageStorageSettings, siteId).CreateFolders();
    }
}
