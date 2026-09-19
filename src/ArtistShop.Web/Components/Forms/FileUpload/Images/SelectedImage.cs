using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Components.Forms.FileUpload.Images;

public class SelectedImage(string id, string name, long size) : SelectedFile(id, name, size)
{
    // null while the upload is still running. An image already saved with the artwork has one from
    // the start, because there is nothing left to wait for
    public ArtworkImage? Result { get; set; }

    // The storage key stands in for the browser's file id: an image the artist just dropped is
    // known by the id the drop zone gave it, and a saved one was never in that list. Its size isn't
    // stored, and nothing shows it.
    public static SelectedImage ForSavedImage(ArtworkImage image) =>
        new(image.StorageKey, image.OriginalFileName ?? "Image", size: 0)
        {
            Result = image,
            UploadPercentComplete = 100,
        };
}
