namespace ArtistShop.Web.Domain.Commerce;

public record ProductId(int Value);

public record ProductTypeId(int Value);

public record ProductTypeName(string Value);

public record ProductType(ProductTypeId Id, ProductTypeName Name);

// EditionSize is how many were ever made: null means it can always be restocked, 1 means one of a kind
public record Product(
    ProductId Id,
    ProductType Type,
    string? Label,
    decimal? Price,
    int? EditionSize,
    int Stock
);

public record ProductAddition(
    ProductTypeId TypeId,
    string? Label,
    decimal? Price,
    int? EditionSize,
    int Stock
);
