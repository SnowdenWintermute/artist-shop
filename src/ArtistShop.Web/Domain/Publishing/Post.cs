namespace ArtistShop.Web.Domain.Publishing;

using System.Text.Json;

public record PostId(int Value);

public record PostTitle(string Value);

public record PostSlug(string Value)
{
    public static PostSlug FromTitle(string title) => new(ArtistShopSlug.FromName(title));
}

// the editor's document as JSON text: a Quill Delta, {"ops": [...]}
public record PostBody(string Json)
{
    // the document Quill starts from: one empty line
    public static readonly PostBody Empty = new("""{"ops":[{"insert":"\n"}]}""");

    // what check_posts_body_ops requires, checked before the database gets to refuse it
    public static bool IsDelta(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            return document.RootElement.ValueKind is JsonValueKind.Object
                && document.RootElement.TryGetProperty("ops", out var ops)
                && ops.ValueKind is JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public enum PostStatus
{
    Draft,
    Published,
}

// PublishedAt is null for a draft
public record Post(
    PostId Id,
    PostTitle Title,
    PostSlug Slug,
    PostBody Body,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record PostSummary(
    PostId Id,
    PostTitle Title,
    PostSlug Slug,
    DateTimeOffset? PublishedAt,
    DateTimeOffset UpdatedAt
);

// a post on the public blog list, with its body for the excerpt
public record PostListItem(PostId Id, PostTitle Title, PostSlug Slug, PostBody Body, DateTimeOffset PublishedAt);

public record PostListPage(IReadOnlyList<PostListItem> Items, int TotalCount, int PageNumber, int PageSize)
{
    public int PageCount => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
}
