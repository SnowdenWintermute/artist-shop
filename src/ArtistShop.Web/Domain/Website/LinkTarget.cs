namespace ArtistShop.Web.Domain.Website;

using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;

// The ids are only for ordering; settings store the names
public enum LinkTargetType
{
    Page = 1,
    Work = 2,
    Collection = 3,
    Post = 4,
    Address = 5,
}

// Where clicking a component goes: one of the website's own pages, works, collections or posts, by id so a
// changed address still reaches it, or any address. Opened in the same tab, as any link is
public abstract record LinkTarget
{
    public abstract LinkTargetType Type { get; }

    public sealed record Page(CustomPageId Id) : LinkTarget
    {
        public override LinkTargetType Type => LinkTargetType.Page;
    }

    public sealed record Work(WorkId Id) : LinkTarget
    {
        public override LinkTargetType Type => LinkTargetType.Work;
    }

    public sealed record Collection(CollectionId Id) : LinkTarget
    {
        public override LinkTargetType Type => LinkTargetType.Collection;
    }

    public sealed record Post(PostId Id) : LinkTarget
    {
        public override LinkTargetType Type => LinkTargetType.Post;
    }

    public sealed record Address(string Value) : LinkTarget
    {
        public override LinkTargetType Type => LinkTargetType.Address;

        // a web page's whole address, or an email's
        public static bool Allows(string value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeMailto);
    }
}
