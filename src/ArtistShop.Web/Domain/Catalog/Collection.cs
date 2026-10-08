
namespace ArtistShop.Web.Domain.Catalog;

public record CollectionId(int Value);

public record CollectionName(string Value);

public record CollectionSlug(string Value)
{
    public static CollectionSlug FromName(string name) => new(ArtistShopSlug.FromName(name));
}

public record Collection(CollectionId Id, CollectionName Name, CollectionSlug Slug);

// Cover is null when no work in the collection has an image
public record CollectionWithCover(
    CollectionId Id,
    CollectionName Name,
    CollectionSlug Slug,
    int WorkCount,
    WorkImage? Cover
);

public record CollectionWork(
    WorkId Id,
    WorkName Name,
    WorkTypeName WorkTypeName,
    bool IsCover,
    WorkImage? PrimaryImage
);

public record CollectionWithWorks(
    CollectionId Id,
    CollectionName Name,
    CollectionSlug Slug,
    IReadOnlyList<CollectionWork> Works
);
