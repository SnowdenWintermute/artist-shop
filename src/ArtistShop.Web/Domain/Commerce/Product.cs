namespace ArtistShop.Web.Domain.Commerce;

public record ProductId(int Value);

public record ProductKindId(int Value);

public record ProductKindName(string Value);

public record ProductKind(ProductKindId Id, ProductKindName Name);

// EditionSize is how many were ever made: null means it can always be restocked, 1 means one of a kind
public record Product(
    ProductId Id,
    ProductKind Kind,
    string? Label,
    decimal? Price,
    int? EditionSize,
    int Stock
);

public record ProductAddition(
    ProductKindId KindId,
    string? Label,
    decimal? Price,
    int? EditionSize,
    int Stock
);
