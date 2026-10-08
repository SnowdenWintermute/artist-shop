namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Website;
using Dapper;

public class WordingRepository(SiteDatabase database)
{
    public async Task<SiteWording> GetAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        return ToWording(await connection.QuerySingleAsync<WordingRow>("SELECT * FROM get_wording()"));
    }

    // Blocking, for the cascade in Program.cs: a cascading value is made in the middle of a render,
    // which can't wait for anything
    public SiteWording Get()
    {
        using var connection = database.OpenConnection();

        return ToWording(connection.QuerySingle<WordingRow>("SELECT * FROM get_wording()"));
    }

    public async Task UpdateAsync(SiteWording wording)
    {
        await using var connection = await database.OpenConnectionAsync();

        await connection.ExecuteAsync(
            """
            SELECT update_wording(
                @CollectionSingular, @CollectionPlural, @CollectionKeepsCase,
                @WorkSingular, @WorkPlural, @WorkKeepsCase
            )
            """,
            new
            {
                CollectionSingular = wording.CollectionChoice.Singular,
                CollectionPlural = wording.CollectionChoice.Plural,
                CollectionKeepsCase = wording.CollectionChoice.KeepsCase,
                WorkSingular = wording.WorkChoice.Singular,
                WorkPlural = wording.WorkChoice.Plural,
                WorkKeepsCase = wording.WorkChoice.KeepsCase,
            }
        );
    }

    private static SiteWording ToWording(WordingRow row) =>
        new(
            new NounChoice(row.CollectionSingular, row.CollectionPlural, row.CollectionKeepsCase),
            new NounChoice(row.WorkSingular, row.WorkPlural, row.WorkKeepsCase)
        );

    private sealed class WordingRow
    {
        public required string? CollectionSingular { get; init; }
        public required string? CollectionPlural { get; init; }
        public required bool CollectionKeepsCase { get; init; }
        public required string? WorkSingular { get; init; }
        public required string? WorkPlural { get; init; }
        public required bool WorkKeepsCase { get; init; }
    }
}
