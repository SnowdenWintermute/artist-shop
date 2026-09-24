namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using Dapper;
using Npgsql;

public class PostRepository(NpgsqlDataSource dataSource)
{
    private const string UniqueSlugConstraint = "unique_posts_slug";

    private static bool IsSlugTaken(PostgresException exception) =>
        SqlErrors.IsUniqueConstraintViolation(exception, UniqueSlugConstraint);

    // the images uploaded into posts, which live only in the bodies, for the orphan sweep
    public async Task<HashSet<string>> GetAllImageStorageKeysAsync()
    {
        await using var connection = dataSource.CreateConnection();

        var storageKeys = await connection.QueryAsync<string>("SELECT * FROM get_all_post_image_storage_keys()");

        return [.. storageKeys];
    }

    public async Task<Post?> GetAsync(PostId id)
    {
        await using var connection = dataSource.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<PostRow>(
            "SELECT * FROM get_post(@Id)",
            new { Id = id.Value }
        );

        return row?.ToPost();
    }

    // drafts included: an admin reads a draft on the page it will appear on
    public async Task<Post?> GetBySlugAsync(PostSlug slug)
    {
        await using var connection = dataSource.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<PostRow>(
            "SELECT * FROM get_post_by_slug(@Slug)",
            new { Slug = slug.Value }
        );

        return row?.ToPost();
    }

    public async Task<Post?> GetPublishedBySlugAsync(PostSlug slug)
    {
        await using var connection = dataSource.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<PostRow>(
            "SELECT * FROM get_published_post_by_slug(@Slug)",
            new { Slug = slug.Value }
        );

        return row?.ToPost();
    }

    // drafts included, for the admin list
    public async Task<List<PostSummary>> GetAllAsync()
    {
        await using var connection = dataSource.CreateConnection();

        var rows = await connection.QueryAsync<PostSummaryRow>("SELECT * FROM get_post_list()");

        return [.. rows.Select(row => row.ToPostSummary())];
    }

    // the count rides on the rows, so a page past the end carries none; the page sends a page
    // number past the end back to the first page
    public async Task<PostListPage> GetPublishedPageAsync(int pageNumber)
    {
        await using var connection = dataSource.CreateConnection();

        var pageSize = ArtistShopLimits.BlogPageSize;

        var rows = (
            await connection.QueryAsync<PostListRow>(
                "SELECT * FROM get_published_post_page(@Offset, @PageSize)",
                new { Offset = (pageNumber - 1) * pageSize, PageSize = pageSize }
            )
        ).ToList();

        var totalCount = rows.Count is 0 ? 0 : rows[0].TotalCount;

        return new PostListPage([.. rows.Select(row => row.ToPostListItem())], totalCount, pageNumber, pageSize);
    }

    public async Task<List<PostSummary>> GetPublishedMentioningArtworkAsync(ArtworkId artworkId)
    {
        await using var connection = dataSource.CreateConnection();

        var rows = await connection.QueryAsync<PostSummaryRow>(
            "SELECT * FROM get_published_posts_mentioning_artwork(@ArtworkId)",
            new { ArtworkId = artworkId.Value }
        );

        return [.. rows.Select(row => row.ToPostSummary())];
    }

    // Npgsql sends a C# string as text, and Postgres has no implicit cast from text to jsonb, so
    // the body is cast in the call or the function isn't found
    public async Task<PostId> AddAsync(PostTitle title, PostSlug slug, PostBody body, PostStatus status)
    {
        await using var connection = dataSource.CreateConnection();

        try
        {
            var id = await connection.QuerySingleAsync<int>(
                "SELECT add_post(@Title, @Slug, CAST(@Body AS jsonb), @IsPublished)",
                new
                {
                    Title = title.Value,
                    Slug = slug.Value,
                    Body = body.Json,
                    IsPublished = status is PostStatus.Published,
                }
            );

            return new PostId(id);
        }
        catch (PostgresException exception) when (IsSlugTaken(exception))
        {
            throw new NameAlreadyInUseException(title.Value);
        }
    }

    public async Task UpdateAsync(
        PostId id,
        PostTitle title,
        PostSlug slug,
        PostBody body,
        PostStatus status
    )
    {
        await using var connection = dataSource.CreateConnection();

        try
        {
            await connection.ExecuteAsync(
                "SELECT update_post(@Id, @Title, @Slug, CAST(@Body AS jsonb), @IsPublished)",
                new
                {
                    Id = id.Value,
                    Title = title.Value,
                    Slug = slug.Value,
                    Body = body.Json,
                    IsPublished = status is PostStatus.Published,
                }
            );
        }
        catch (PostgresException exception) when (IsSlugTaken(exception))
        {
            throw new NameAlreadyInUseException(title.Value);
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.PostNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    public async Task DeleteAsync(PostId id)
    {
        await using var connection = dataSource.CreateConnection();

        await connection.ExecuteAsync("SELECT delete_post(@Id)", new { Id = id.Value });
    }

    // Npgsql reads a timestamptz as a DateTime in UTC, which is what these hold
    private sealed class PostRow
    {
        public required int Id { get; init; }
        public required string Title { get; init; }
        public required string Slug { get; init; }
        public required string Body { get; init; }
        public DateTime? PublishedAt { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }

        public Post ToPost() =>
            new(
                new PostId(Id),
                new PostTitle(Title),
                new PostSlug(Slug),
                new PostBody(Body),
                PublishedAt is DateTime publishedAt ? new DateTimeOffset(publishedAt) : null,
                new DateTimeOffset(CreatedAt),
                new DateTimeOffset(UpdatedAt)
            );
    }

    private sealed class PostSummaryRow
    {
        public required int Id { get; init; }
        public required string Title { get; init; }
        public required string Slug { get; init; }
        public DateTime? PublishedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }

        public PostSummary ToPostSummary() =>
            new(
                new PostId(Id),
                new PostTitle(Title),
                new PostSlug(Slug),
                PublishedAt is DateTime publishedAt ? new DateTimeOffset(publishedAt) : null,
                new DateTimeOffset(UpdatedAt)
            );
    }

    private sealed class PostListRow
    {
        public required int Id { get; init; }
        public required string Title { get; init; }
        public required string Slug { get; init; }
        public required string Body { get; init; }
        public required DateTime PublishedAt { get; init; }
        public required int TotalCount { get; init; }

        public PostListItem ToPostListItem() =>
            new(
                new PostId(Id),
                new PostTitle(Title),
                new PostSlug(Slug),
                new PostBody(Body),
                new DateTimeOffset(PublishedAt)
            );
    }
}
