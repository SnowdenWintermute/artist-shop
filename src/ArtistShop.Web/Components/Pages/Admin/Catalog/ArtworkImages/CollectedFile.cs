namespace ArtistShop.Web.Components.Pages.Admin.Catalog.ArtworkImages;

// what the browser found about one file. Path includes the dropped or picked folder's own name, so
// "/Seascapes/1990s/sunset.jpg" is two folders deep; a drop spells it with a leading slash, and a
// file that came in on its own is just its name. Type is the browser's guess from the extension
public record CollectedFile(string Id, string Path, long Size, string Type);
