namespace ArtistShop.Web.Domain.Sites;

// A site as the operator's list of every site shows it. OwnerUserId is null once the owner deleted
// their account, for the rest of the grace period that started; EraseAt is when it's erased, if it's
// being deleted; ExampleSortOrder is its place among the home page's examples, if it's one
public record SiteListing(
    SiteId SiteId,
    HostName MainHost,
    IReadOnlyList<HostName> OtherHosts,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EraseAt,
    int? ExampleSortOrder,
    string? OwnerUserId,
    int AdminCount
)
{
    public bool IsBeingDeleted => EraseAt is not null;

    public bool IsExample => ExampleSortOrder is not null;
}
