namespace ArtistShop.Web.Components.Pages.Admin.Catalog.ArtworkImages;

// what would happen, or did happen, to one file. The name pre-check sets all of these but
// Attached and Failed, and the server's answer replaces it once the file is uploaded
public enum BulkImageOutcome : byte
{
    WillAttach = 1,
    Attached = 2,
    NoArtwork = 3,
    SeveralArtworks = 4,
    ArtworkHasImages = 5,
    DuplicateNameInUpload = 6,
    Failed = 7,
}
