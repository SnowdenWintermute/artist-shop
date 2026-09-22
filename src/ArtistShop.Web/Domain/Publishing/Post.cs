namespace ArtistShop.Web.Domain.Publishing;

public record PostId(int Value);

public record PostTitle(string Value);

public record PostSlug(string Value)
{
    public static PostSlug FromTitle(string title) => new(ArtistShopSlug.FromName(title));
}

// the editor's document as JSON text: a Quill Delta, {"ops": [...]}
public record PostBody(string Json);

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
