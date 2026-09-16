namespace ArtistShop.Web.Domain.Catalog;

public record ArtworkTypeId(int Value);

public record ArtworkTypeName(string Value);

public record ArtworkType(ArtworkTypeId Id, ArtworkTypeName Name);
