namespace ArtistShop.Web.Domain.Catalog;

// what it takes to link to a work: the name to show and the slug to point at
public record WorkLink(WorkName Name, WorkSlug Slug);

// a work at a place in a collection, and which collection, since that goes in its address. The image
// count is so that stepping back past a work's first image can land on the previous one's last
public record WorkInCollection(WorkSlug Slug, CollectionSlug CollectionSlug, int ImageCount);

// the places either side, running on into the collection before or after; null on either side is the
// first place of the first collection or the last of the last
public record WorkNeighbours(WorkInCollection? Previous, WorkInCollection? Next);

// where a work's images sit among all those Previous and Next step through: how many come
// before its first, and how many there are altogether. The lightbox numbers its pictures by it
public record WorkWalkPosition(int EarlierImageCount, int TotalImageCount);
