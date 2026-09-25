namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Platform;
using Dapper;
using Npgsql;

// the platform database's sign-up codes, kept as hashes
public class SignUpCodeRepository(NpgsqlDataSource platformDataSource)
{
    public async Task<SignUpCodeId> AddAsync(SignUpCode code, string note, DateTimeOffset expiresAt)
    {
        await using var connection = platformDataSource.CreateConnection();

        var id = await connection.ExecuteScalarAsync<int>(
            "SELECT add_sign_up_code(@CodeHash, @Note, @ExpiresAt)",
            new { CodeHash = code.Hash(), Note = note, ExpiresAt = expiresAt }
        );

        return new SignUpCodeId(id);
    }

    // unused and not expired. Sign-up asks before making the site's schema; using the code checks again
    public async Task<bool> IsUsableAsync(SignUpCode code)
    {
        await using var connection = platformDataSource.CreateConnection();

        return await connection.ExecuteScalarAsync<bool>(
            "SELECT sign_up_code_is_usable(@CodeHash)",
            new { CodeHash = code.Hash() }
        );
    }

    public async Task<List<SignUpCodeListing>> GetAllAsync()
    {
        await using var connection = platformDataSource.CreateConnection();

        var rows = await connection.QueryAsync<SignUpCodeRow>("SELECT * FROM get_sign_up_codes()");

        return [.. rows.Select(row => row.ToListing())];
    }

    public async Task DeleteAsync(SignUpCodeId id)
    {
        await using var connection = platformDataSource.CreateConnection();

        await connection.ExecuteAsync("SELECT delete_sign_up_code(@Id)", new { Id = id.Value });
    }

    // Npgsql reads a timestamptz as a DateTime in UTC, which is what these hold
    private sealed class SignUpCodeRow
    {
        public required int Id { get; init; }
        public required string Note { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime ExpiresAt { get; init; }

        public SignUpCodeListing ToListing() =>
            new(new SignUpCodeId(Id), Note, new DateTimeOffset(CreatedAt), new DateTimeOffset(ExpiresAt));
    }
}
