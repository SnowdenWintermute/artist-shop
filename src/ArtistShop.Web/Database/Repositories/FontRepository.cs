namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Website;
using Dapper;
using Npgsql;

// the platform database's order of the fonts, stored by Font name
public class FontRepository(NpgsqlDataSource platformDataSource)
{
    // the fonts the operator placed, in their order; Fonts.InOrder adds the rest
    public async Task<IReadOnlyList<Font>> GetOrderAsync()
    {
        await using var connection = platformDataSource.CreateConnection();

        var names = await connection.QueryAsync<string>("SELECT font FROM get_font_order()");

        var placed = new List<Font>();

        foreach (var name in names)
        {
            // TryParse would also take a number
            if (Enum.TryParse<Font>(name, out var font) && font.ToString() == name)
            {
                placed.Add(font);
            }
        }

        return placed;
    }

    public async Task ReorderAsync(IReadOnlyList<Font> fonts)
    {
        await using var connection = platformDataSource.CreateConnection();

        await connection.ExecuteAsync(
            "SELECT reorder_fonts(@Fonts)",
            new { Fonts = (string[])[.. fonts.Select(font => font.ToString())] }
        );
    }
}
