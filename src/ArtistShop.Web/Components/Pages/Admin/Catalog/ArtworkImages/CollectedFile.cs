namespace ArtistShop.Web.Components.Pages.Admin.Catalog.ArtworkImages;

// what the browser found about one file. Path is relative to the folder that was dropped, and
// Type is the browser's guess from the extension
public record CollectedFile(string Id, string Path, long Size, string Type);
