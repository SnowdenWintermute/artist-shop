namespace ArtistShop.Web.Database;

// the composite types in Scripts/0002_CreateInputTypes.sql. SiteDataSource maps each one, and
// Npgsql matches a field to the constructor parameter of the same name in snake_case
public sealed record ArtworkImageInput(
    string StorageKey,
    string? OriginalFileName,
    int SortOrder,
    bool IsPrimary,
    int Width,
    int Height,
    string? BlurDataUri
);

public sealed record ProductInput(
    int ProductTypeId,
    string? Label,
    decimal? Price,
    int? EditionSize,
    int Stock
);

public sealed record SeriesNameAndSlug(string Name, string Slug);
