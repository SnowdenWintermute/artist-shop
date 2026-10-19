namespace ArtistShop.Web.Images;

// The widths a picture may load now, and the widest it will have: ImageSizeQueue.ReadyWidths and
// LargestWidthFor, plus for a full-bleed picture ImageVariants.FullBleedWidthFor once it's made
public record PictureWidths(int[] Ready, int Largest);

// The large size full-bleed pictures load, made on demand: a page drawing one without it queues it, and
// uses the sizes there are until it's made. The stored files are the record of what's made, as for
// every size, so a restart that lost the job is made good by the next page drawing the picture
public class FullBleedImageSizes(ImageSizeQueue imageSizeQueue, ImageStorage imageStorage)
{
    public PictureWidths For(string storageKey, int imageWidth, bool isFullBleed)
    {
        var ready = imageSizeQueue.ReadyWidths(storageKey, imageWidth);
        var largest = ImageVariants.LargestWidthFor(imageWidth);

        if (!isFullBleed || ImageVariants.FullBleedWidthFor(imageWidth) is not int fullBleedWidth)
        {
            return new PictureWidths(ready, largest);
        }

        if (imageSizeQueue.PendingWidths(storageKey).Contains(fullBleedWidth))
        {
            return new PictureWidths(ready, fullBleedWidth);
        }

        var fileNames = imageStorage.VariantFileNames(storageKey);

        if (Enum.GetValues<ImageVariantFormat>().All(format => fileNames.Contains(ImageVariants.FileName(fullBleedWidth, format))))
        {
            return new PictureWidths([.. ready, fullBleedWidth], fullBleedWidth);
        }

        imageSizeQueue.Enqueue(new ImageSizeJob(imageStorage, storageKey, [fullBleedWidth]));

        return new PictureWidths(ready, fullBleedWidth);
    }
}
