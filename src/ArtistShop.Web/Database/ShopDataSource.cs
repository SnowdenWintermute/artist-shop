namespace ArtistShop.Web.Database;

using Npgsql;

// the app and the tests both build their data source here, so both know the composite types
public static class ShopDataSource
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
