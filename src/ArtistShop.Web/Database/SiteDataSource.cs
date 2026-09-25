namespace ArtistShop.Web.Database;

using ArtistShop.Web.Sites;
using Npgsql;

// The one pool every site's schema is reached through (SiteDatabase), for the app, the tools and
// the tests alike, so all know the composite types
public static class SiteDataSource
{
    public static NpgsqlDataSource Create(string platformConnectionString, SiteDatabaseSettings settings)
    {
        var builder = new NpgsqlDataSourceBuilder(platformConnectionString)
        {
            ConnectionStringBuilder =
            {
                // where a connection's search_path goes back to as it's returned to the pool: no
                // site's schema, so a connection that hasn't been given one finds no site's functions
                SearchPath = SiteSchema.TypesSchema,
                MaxPoolSize = settings.MaximumPoolSize,
                // in seconds, as Npgsql counts it
                ConnectionIdleLifetime = (int)settings.ConnectionIdleLifetime.TotalSeconds,
            },
        };
        builder.MapComposite<ArtworkImageInput>($"{SiteSchema.TypesSchema}.artwork_image_input");
        builder.MapComposite<ProductInput>($"{SiteSchema.TypesSchema}.product_input");
        builder.MapComposite<SeriesNameAndSlug>($"{SiteSchema.TypesSchema}.series_name_and_slug");
        return builder.Build();
    }
}
