namespace ArtistShop.Web.Database;

using Npgsql;

// a site database's data source, for the app and the tests alike, so both know the composite types
public static class SiteDataSource
{
    public static NpgsqlDataSource Create(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.MapComposite<ArtworkImageInput>("artwork_image_input");
        builder.MapComposite<ProductInput>("product_input");
        builder.MapComposite<SeriesNameAndSlug>("series_name_and_slug");
        return builder.Build();
    }
}
