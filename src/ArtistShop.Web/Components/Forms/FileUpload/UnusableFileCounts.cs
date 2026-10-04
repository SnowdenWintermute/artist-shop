using ArtistShop.Web.Images;

namespace ArtistShop.Web.Components.Forms.FileUpload;

// the files in a drop of images that can't be used: HEIC, which the image library here can't read,
// and anything that isn't an image format we take
public record UnusableFileCounts(int Heic, int NotImage)
{
    public static readonly UnusableFileCounts None = new(0, 0);

    public bool Any => Heic > 0 || NotImage > 0;

    public static UnusableFileCounts Of(IReadOnlyCollection<CollectedFile> files) =>
        new(
            files.Count(file => ImageUploadValidation.IsHeic(file.Type)),
            files.Count(file => !ImageUploadValidation.IsHeic(file.Type) && !ImageUploadValidation.IsPermittedContentType(file.Type))
        );
}
