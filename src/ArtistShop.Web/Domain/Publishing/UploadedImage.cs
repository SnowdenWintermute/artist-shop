namespace ArtistShop.Web.Domain.Publishing;

using ArtistShop.Web.Domain.Catalog;

public record UploadedImageId(int Value);

// An image in the library. AltText is empty when the artist cleared it
public record UploadedImage(
    UploadedImageId Id,
    string StorageKey,
    string OriginalFileName,
    int Width,
    int Height,
    string BlurDataUri,
    string AltText,
    DateTimeOffset UploadedAt
)
{
    // what it's called in lists: its alt text, or its file name when it has none
    public string Name => AltText is "" ? OriginalFileName : AltText;

    // what TileImage draws a thumbnail from, a work's image or not
    public WorkImage TileImage => new(StorageKey, OriginalFileName, Width, Height, BlurDataUri);
}

// UsedInCount is how many posts and text components show it
public record UploadedImageListItem(UploadedImage Image, int UsedInCount);

public record UploadedImageListPage(IReadOnlyList<UploadedImageListItem> Items, int TotalCount, int PageNumber, int PageSize)
{
    public int PageCount => Paging.PageCountOf(TotalCount, PageSize);
}

public enum UploadedImageUseType
{
    Post,
    Component,
}

// a post or text component showing a library image. PageNames are the pages a component is on
public record UploadedImageUse(UploadedImageUseType Type, string Name, IReadOnlyList<string> PageNames);
