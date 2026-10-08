namespace ArtistShop.Web.Components.Catalog;

using ArtistShop.Web.Domain.Catalog;

public static class WorkTypeOrder
{
    public static List<WorkType> SortedByName(IEnumerable<WorkType> workTypes) =>
        [.. workTypes.OrderBy(workType => workType.Name.Value, NameOrder.ByName)];
}
