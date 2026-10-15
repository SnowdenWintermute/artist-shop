namespace ArtistShop.Web.Components;

using ArtistShop.Web.Domain.Website;

// The row editor a component's settings page was opened from, kept in the address as PageUrls.ComponentPageKey
// and ComponentRowKey, so the settings page and the pages it leads to can go back to it
public record CustomComponentOrigin(CustomPageId PageId, PageRowId RowId)
{
    // null unless the address has both
    public static CustomComponentOrigin? Of(int? pageId, int? rowId) =>
        pageId is int page && rowId is int row ? new CustomComponentOrigin(new CustomPageId(page), new PageRowId(row)) : null;
}
