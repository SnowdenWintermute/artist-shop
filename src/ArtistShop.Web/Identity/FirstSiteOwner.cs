namespace ArtistShop.Web.Identity;

public static class FirstSiteOwner
{
    // The account FirstSite:OwnerEmail names, made with FirstSite:OwnerPassword if it doesn't exist
    // yet. Only asked for while there are no sites, so both settings can go once the first is made
    public static async Task<ApplicationUser> FindOrCreateAsync(IServiceProvider services)
    {
        var email = services.GetRequiredService<IConfiguration>()["FirstSite:OwnerEmail"];

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException(
                "There are no sites yet, and FirstSite:OwnerEmail names no account to own the first one."
            );
        }

        return await SeededAccounts.FindOrCreateAsync(services, email, "FirstSite:OwnerPassword");
    }
}
