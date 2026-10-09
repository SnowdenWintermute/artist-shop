namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Website;
using Dapper;

public class ColorRepository(SiteDatabase database)
{
    public async Task<SiteColors> GetAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<ColorRow>("SELECT * FROM get_colors()");

        return new SiteColors(rows.ToDictionary(row => row.ColorRoleId, row => RgbColor.Parse(row.Color)));
    }

    public async Task UpdateAsync(SiteColors colors)
    {
        await using var connection = await database.OpenConnectionAsync();
        // one list, so the two arrays pair up in the same order
        var chosen = colors.Chosen.ToList();

        await connection.ExecuteAsync(
            "SELECT update_colors(@ColorRoleIds, @Colors)",
            new
            {
                ColorRoleIds = chosen.Select(entry => (int)entry.Key).ToArray(),
                Colors = chosen.Select(entry => entry.Value.Hex).ToArray(),
            }
        );
    }

    private sealed class ColorRow
    {
        public required ColorRole ColorRoleId { get; init; }
        public required string Color { get; init; }
    }
}
