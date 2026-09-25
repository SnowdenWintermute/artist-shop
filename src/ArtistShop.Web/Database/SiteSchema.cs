namespace ArtistShop.Web.Database;

using System.Globalization;
using ArtistShop.Web.Domain.Sites;

// Each site's data is a schema of its own in the platform database, site_<id>
public static class SiteSchema
{
    // the collations and input types every site's schema uses (Platform/Scripts/0004_CreateSiteTypes.sql)
    public const string TypesSchema = "site_types";

    public static string Name(SiteId siteId) => $"site_{siteId.Value.ToString(CultureInfo.InvariantCulture)}";

    // the site's own schema first, where its tables and functions are made and then found, then the
    // types they share
    public static string SearchPath(SiteId siteId) => $"{Name(siteId)}, {TypesSchema}";
}
