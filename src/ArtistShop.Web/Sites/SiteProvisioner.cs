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
    // listed is always ready, and if anything fails its schema and folders are removed and nothing,
    // such as a sign-up code, is used up. A crash in between leaves them for
    // RemoveUnlistedAsync at the next startup
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
            ImageStorage.ForSite(imageStorageSettings, siteId).DeleteAll();
            throw;
        }

        return siteId;
    }

    // Schemas and image folders of sites that aren't listed: left by a sign-up that crashed after
    // making them. Only at startup, before any request, since a sign-up in progress has them
    // before its site is listed
    public async Task RemoveUnlistedAsync()
    {
        var listed = (await siteRepository.GetIdsAsync()).ToHashSet();

        foreach (var siteId in (await siteSchemas.GetSiteIdsAsync()).Where(siteId => !listed.Contains(siteId)))
        {
            await siteSchemas.DropAsync(siteId);
        }

        foreach (var siteId in ImageStorage.SiteIdsWithFolders(imageStorageSettings).Where(siteId => !listed.Contains(siteId)))
        {
            ImageStorage.ForSite(imageStorageSettings, siteId).DeleteAll();
        }
    }

    // Safe to run again, and run at every startup for every site, so a new migration reaches every
    // site
    public void Prepare(SiteId siteId)
    {
        siteSchemas.Migrate(siteId);
        ImageStorage.ForSite(imageStorageSettings, siteId).CreateFolders();
    }
}
