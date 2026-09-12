using ArtistShop.Web.Images;

namespace ArtistShop.Web.Components.Forms.FileUpload.Images;

public class SelectedImage(string id, string name, long size) : SelectedFile(id, name, size)
{
    public ImageUploadResult? Result { get; set; }
}
