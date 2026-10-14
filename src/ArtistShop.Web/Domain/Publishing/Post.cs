namespace ArtistShop.Web.Domain.Publishing;

public record PostId(int Value);

public record PostTitle(string Value)
{
    // A copy's title: "Title (copy)", then "Title (copy 2)" and on while those are taken. Kept short
    // enough that its slug is never cut, or every number past the cut would make the same slug
    public PostTitle ForCopy(int number)
    {
        var suffix = number is 1 ? " (copy)" : $" (copy {number})";
        var room = ArtistShopLimits.BaseSlugMaximumLength - suffix.Length;

        return new((Value.Length > room ? Value[..room].TrimEnd() : Value) + suffix);
    }
}

public record PostSlug(string Value)
{
    public static PostSlug FromTitle(string title) => new(ArtistShopSlug.FromName(title));
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
    RichText Body,
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

// a published post that embeds a work, for the work's page
public record PostMention(PostId Id, PostTitle Title, PostSlug Slug, DateTimeOffset PublishedAt);

// a post on the public blog list, with its body for the excerpt
public record PostListItem(PostId Id, PostTitle Title, PostSlug Slug, RichText Body, DateTimeOffset PublishedAt);

public record PostListPage(IReadOnlyList<PostListItem> Items, int TotalCount, int PageNumber, int PageSize)
{
    public int PageCount => Paging.PageCountOf(TotalCount, PageSize);
}
