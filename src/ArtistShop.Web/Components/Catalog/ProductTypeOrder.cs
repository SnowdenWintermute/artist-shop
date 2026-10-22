namespace ArtistShop.Web.Components.Catalog;

using ArtistShop.Web.Domain.Commerce;
using ArtistShop.Web.Utilities;

public static class ProductTypeOrder
{
    public static List<ProductType> SortedByName(IEnumerable<ProductType> productTypes) =>
        [.. productTypes.OrderBy(productType => productType.Name.Value, NameOrder.ByName)];
}
