namespace ArtistShop.Web.Domain;

public static class Paging
{
    // an empty list still has its one, empty, page
    public static int PageCountOf(int totalCount, int pageSize) => Math.Max(1, (totalCount + pageSize - 1) / pageSize);
}
